using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LBAAssembler.Terrain;

namespace LBRAInstaller;

// Racing > Shared tracks > "Make a track...": a race track made in the game's own setup, no LBA Assembler needed (the user, 2026-11-04:
// make track making "more accessible and user friendly"). The island's map seen from above, LBA Assembler's track pieces laid on it
// (TrackPieceSet: snapped on from the open ends, at their heights; starters; Close the lap; the check as it goes), and Save: the pieces
// made into a plan (TrackPieceSet.MakePlan), written as a .lbrtrack into the player's shared tracks folder, which the shared tracks window
// then builds into the game as any friend's track.
internal sealed class TrackMakerWindow : Window
{
    private const int MapScale = 3;
    private readonly string installDir;
    private readonly ComboBox islandBox = new() { MinWidth = 260 };
    private readonly Canvas board = new();
    private readonly Image mapImage = new() { Stretch = Stretch.None };
    private readonly Canvas overlay = new() { IsHitTestVisible = false };
    private readonly StackPanel checks = new() { Margin = new Thickness(0, 6, 0, 0) };
    private readonly TextBlock said = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBox nameBox = new() { Text = "My track", MaxLength = RaceTrackShare.MaxName, Padding = new Thickness(3) };
    private readonly ScaleTransform zoom = new(1, 1);
    private readonly TrackPieceSet pieces = new();
    private readonly Stack<List<TrackPiece>> undo = new();
    private RaceTrackIsland? island;
    private IslandFile? ground;
    private int originX, originZ;
    private TrackSection? armed;
    private TrackPiece? ghost, selected;
    private double heading;
    // how high over the ground a piece being laid goes where it joins nothing (PgUp, PgDn), and the Ramp's climb (R, F)
    private double lift, tilt;
    // the .lbrtrack written by Save, or null
    public string? Saved { get; private set; }

    public TrackMakerWindow(string installDir)
    {
        this.installDir = installDir;
        UpdateLook.Dress(this, $"{GameInstall.Title} - Make a track");
        Width = 1180; Height = 780; ResizeMode = ResizeMode.CanResize; SizeToContent = SizeToContent.Manual;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (Environment.GetEnvironmentVariable("LBRA_SETUP_WINDOW_POS") is { } pos && pos.Split(',') is [var x, var y]
            && double.TryParse(x, out var px) && double.TryParse(y, out var py)) { WindowStartupLocation = WindowStartupLocation.Manual; Left = px; Top = py; }

        var dock = new DockPanel { Margin = new Thickness(14) };
        // the top: the island
        var top = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        top.Children.Add(new TextBlock { Text = "Island  ", VerticalAlignment = VerticalAlignment.Center });
        foreach (var i in RaceTrackIsland.BuiltIn.Where(i => i.CustomMap is null && IsRetail(i)).GroupBy(i => i.Shown).Select(g => g.First()))
            islandBox.Items.Add(new ComboBoxItem { Content = i.Shown, Tag = i });
        islandBox.SelectionChanged += (_, _) => { if (islandBox.SelectedItem is ComboBoxItem { Tag: RaceTrackIsland i }) LoadIsland(i); };
        top.Children.Add(islandBox);
        top.Children.Add(new TextBlock { Text = "   Ctrl + wheel zooms the map", VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 });
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);

        // the left: the pieces and what to do with them
        var left = new StackPanel { Width = 250, Margin = new Thickness(0, 0, 12, 0) };
        left.Children.Add(Heading("Pieces: choose one, click on the map"));
        var wrap = new WrapPanel();
        foreach (var s in TrackSection.Laid)
        {
            var b = new Button { Content = s.Name, ToolTip = s.Tip, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(6, 2, 6, 2) };
            b.Click += (_, _) => { armed = s; selected = null; heading = pieces.OpenEnds().LastOrDefault().Heading; Say($"{s.Name}: click on the map -- near a yellow ring it joins on. Q / E turn it, Esc stops."); Draw(); };
            wrap.Children.Add(b);
        }
        left.Children.Add(wrap);
        var tools = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
        tools.Children.Add(Small("Turn (Q)", () => Turn(-1)));
        tools.Children.Add(Small("Turn (E)", () => Turn(1)));
        tools.Children.Add(Small("Up (PgUp)", () => Lift(1)));
        tools.Children.Add(Small("Down (PgDn)", () => Lift(-1)));
        tools.Children.Add(Small("Ramp up (R)", () => Tilt(1)));
        tools.Children.Add(Small("Ramp down (F)", () => Tilt(-1)));
        tools.Children.Add(Small("Delete", DeleteSelected));
        tools.Children.Add(Small("Undo", Undo));
        tools.Children.Add(Small("Clear", () => { Remember(); pieces.Pieces.Clear(); selected = null; Changed(); }));
        left.Children.Add(tools);
        left.Children.Add(Heading("Start from"));
        var starter = new ComboBox { Margin = new Thickness(0, 0, 0, 4) };
        foreach (var s in TrackPieceSet.Starters) starter.Items.Add(new ComboBoxItem { Content = s.Name, ToolTip = s.Tip });
        starter.SelectedIndex = 0;
        left.Children.Add(starter);
        var startRow = new WrapPanel();
        startRow.Children.Add(Small("Lay it in the middle", () => LayStarter(TrackPieceSet.Starters[Math.Max(0, starter.SelectedIndex)].Name)));
        startRow.Children.Add(Small("Close the lap", CloseLap));
        left.Children.Add(startRow);
        left.Children.Add(Heading("The track"));
        left.Children.Add(checks);
        left.Children.Add(said);
        DockPanel.SetDock(left, Dock.Left);
        dock.Children.Add(left);

        // the bottom: the name and Save
        var bottom = new DockPanel { Margin = new Thickness(0, 10, 0, 0) };
        var save = new Button { Content = "Save to my shared tracks", MinWidth = 200, IsDefault = true, Background = UpdateLook.Brush("Ember") };
        save.Click += (_, _) => Save();
        var close = new Button { Content = "Close", MinWidth = 90, Margin = new Thickness(10, 0, 0, 0), IsCancel = true };
        DockPanel.SetDock(close, Dock.Right); DockPanel.SetDock(save, Dock.Right);
        bottom.Children.Add(close); bottom.Children.Add(save);
        bottom.Children.Add(new TextBlock { Text = "Its name  ", VerticalAlignment = VerticalAlignment.Center });
        nameBox.Margin = new Thickness(0, 0, 10, 0);
        bottom.Children.Add(nameBox);
        DockPanel.SetDock(bottom, Dock.Bottom);
        dock.Children.Add(bottom);

        // the map
        board.Children.Add(mapImage); board.Children.Add(overlay);
        board.LayoutTransform = zoom;
        board.Background = Brushes.Transparent;
        var scroll = new ScrollViewer { Content = board, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        scroll.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            var z = Math.Clamp(zoom.ScaleX * (e.Delta > 0 ? 1.2 : 1 / 1.2), 0.3, 4);
            zoom.ScaleX = zoom.ScaleY = z; e.Handled = true;
        };
        board.MouseMove += (_, e) => Moved(e.GetPosition(board));
        board.MouseLeftButtonDown += (_, e) => Clicked(e.GetPosition(board));
        board.MouseRightButtonDown += (_, e) => { if (PieceAt(e.GetPosition(board)) is { } p) { Remember(); pieces.Pieces.Remove(p); Changed(); } };
        dock.Children.Add(scroll);
        Content = dock;
        PreviewKeyDown += Key;
        if (islandBox.Items.Count > 0) islandBox.SelectedIndex = 0;
        else Say("No island of the game was found in the install folder.");
    }

    private static TextBlock Heading(string text) => new() { Text = text, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 10, 0, 4) };
    private Button Small(string text, Action click)
    {
        var b = new Button { Content = text, Margin = new Thickness(0, 0, 4, 4), Padding = new Thickness(6, 2, 6, 2) };
        b.Click += (_, _) => click();
        return b;
    }
    private void Say(string text) => said.Text = text;

    // The island as it was before any track went on it -- what a shared track is built on: the install's own islands have the game's
    // tracks built in, so the CD image's file (the LBA2 folder the game was installed from, its disc image -- as the install reads it,
    // GameInstall.RetailFromDisc), else that folder's own file; null: not one of the game's islands (made by the race tracks' build)
    private LBAAssembler.Lba1.Runtime.Lba1Iso? disc;
    private bool discTried;
    private byte[]? RetailIsland(RaceTrackIsland i)
    {
        var backup = System.IO.Path.Combine(installDir, i.IleFile) + RaceTrackService.BackupSuffix;
        if (File.Exists(backup)) return File.ReadAllBytes(backup);
        var lba2 = Updater.Load(installDir)?.Lba2;
        if (lba2 is null || !Directory.Exists(lba2)) return null;
        if (!discTried)
        {
            discTried = true;
            try
            {
                if (Directory.EnumerateFiles(lba2).FirstOrDefault(f => new FileInfo(f).Length > 100_000_000) is { } image) disc = new LBAAssembler.Lba1.Runtime.Lba1Iso(image);
            }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or EndOfStreamException) { disc = null; }
        }
        try
        {
            if (disc?.Files.FirstOrDefault(f => string.Equals(f.Path, "/LBA2/" + i.IleFile, StringComparison.OrdinalIgnoreCase)) is { } entry) return disc.Read(entry);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or EndOfStreamException) { }
        var own = System.IO.Path.Combine(lba2, i.IleFile);
        return File.Exists(own) ? File.ReadAllBytes(own) : null;
    }
    private bool IsRetail(RaceTrackIsland i) => !i.Created && !i.Dream && !i.FromLba1 && File.Exists(System.IO.Path.Combine(installDir, i.IleFile));

    private void LoadIsland(RaceTrackIsland chosen)
    {
        try
        {
            var bytes = RetailIsland(chosen) ?? throw new InvalidDataException("the game's own file of it isn't found (the LBA2 folder the game was installed from)");
            var file = IslandScaler.AsBuilt(IslandFile.Parse(bytes), chosen, new List<string>());
            var renderer = new IslandMapRenderer(file, IslandMapRenderer.LoadPalette(installDir, System.IO.Path.GetFileNameWithoutExtension(chosen.IleFile)), MapScale);
            renderer.RenderAll(MapView.Terrain);
            island = chosen; ground = file; originX = renderer.OriginX; originZ = renderer.OriginZ;
            mapImage.Source = BitmapSource.Create(renderer.PixelWidth, renderer.PixelHeight, 96, 96, PixelFormats.Bgra32, null, renderer.Pixels, renderer.PixelWidth * 4);
            board.Width = renderer.PixelWidth; board.Height = renderer.PixelHeight;
            overlay.Width = renderer.PixelWidth; overlay.Height = renderer.PixelHeight;
            zoom.ScaleX = zoom.ScaleY = Math.Clamp(Math.Min(820.0 / renderer.PixelWidth, 640.0 / renderer.PixelHeight), 0.3, 2);
            pieces.Pieces.Clear(); undo.Clear(); selected = null; armed = null; ghost = null;
            Say($"{chosen.Shown}: lay a starter track, or choose a piece and click on the map.");
            Changed();
        }
        catch (Exception e) when (e is IOException or InvalidDataException or ArgumentException)
        {
            Say($"{chosen.Shown} can't be read: {e.Message}");
        }
    }

    // map pixels and island cells
    private (double X, double Z) Cell(Point p) => (originX + p.X / MapScale, originZ + p.Y / MapScale);
    private Point Pixel(double x, double z) => new((x - originX) * MapScale, (z - originZ) * MapScale);
    private double Ground(double x, double z) => Math.Max(300.0, ground is not null ? IslandOps.Altitude(ground, x * 512, z * 512) ?? 0 : 0);

    private void Moved(Point p)
    {
        if (armed is null) return;
        var (x, z) = Cell(p);
        ghost = pieces.Snapped(new TrackPiece(armed, x, z, Ground(x, z) + lift, heading, armed.Tilts ? tilt : 0));
        Draw();
    }

    private void Clicked(Point p)
    {
        if (armed is not null)
        {
            Moved(p);
            if (ghost is null) return;
            Remember();
            pieces.Pieces.Add(ghost);
            heading = ghost.End.Heading;
            Changed();
            return;
        }
        selected = PieceAt(p);
        Draw();
    }

    private TrackPiece? PieceAt(Point p)
    {
        var (x, z) = Cell(p);
        TrackPiece? best = null; var nearest = 4.0;
        foreach (var piece in pieces.Pieces)
            foreach (var q in piece.Line())
            {
                var d = Math.Sqrt((q.X - x) * (q.X - x) + (q.Z - z) * (q.Z - z));
                if (d < nearest) { nearest = d; best = piece; }
            }
        return best;
    }

    private void Key(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource is TextBox) return;
        switch (e.Key)
        {
            case System.Windows.Input.Key.Q: Turn(-1); e.Handled = true; break;
            case System.Windows.Input.Key.E: Turn(1); e.Handled = true; break;
            case System.Windows.Input.Key.PageUp: Lift(1); e.Handled = true; break;
            case System.Windows.Input.Key.PageDown: Lift(-1); e.Handled = true; break;
            case System.Windows.Input.Key.R: Tilt(1); e.Handled = true; break;
            case System.Windows.Input.Key.F: Tilt(-1); e.Handled = true; break;
            case System.Windows.Input.Key.Delete: DeleteSelected(); e.Handled = true; break;
            case System.Windows.Input.Key.Escape: armed = null; ghost = null; Draw(); e.Handled = true; break;
            case System.Windows.Input.Key.Z when Keyboard.Modifiers == ModifierKeys.Control: Undo(); e.Handled = true; break;
        }
    }

    private void Turn(int way)
    {
        const double step = Math.PI / 12;
        if (armed is not null) { heading += way * step; if (ghost is { } g) ghost = pieces.Snapped(g with { Heading = g.Heading + way * step }); Draw(); return; }
        if (selected is { } s)
        {
            Remember();
            var i = pieces.Pieces.IndexOf(s);
            selected = pieces.Pieces[i] = s with { Heading = s.Heading + way * step };
            Changed();
        }
    }

    // PgUp, PgDn: the piece being laid (where it joins nothing) or the one selected, 250 higher or lower -- a raised road on piers
    private void Lift(int way)
    {
        const double step = 250;
        if (armed is not null) { lift = Math.Max(0, lift + way * step); Say($"Laid {lift:0} over the ground where it joins nothing (a raised road)."); return; }
        if (selected is not { } s) { Say("Choose a piece to lay, or click one to select it, first."); return; }
        Remember();
        var i = pieces.Pieces.IndexOf(s);
        selected = pieces.Pieces[i] = s with { Y = Math.Max(Ground(s.X, s.Z), s.Y + way * step) };
        Changed();
        Say($"{s.Section.Name}: {selected.Y - Ground(s.X, s.Z):0} over the ground.");
    }

    // R, F: the Ramp being laid, or the one selected, 5 % steeper up or down (about its middle)
    private void Tilt(int way)
    {
        if (armed is { Tilts: true }) { tilt = Math.Clamp(tilt + way * TrackPiece.TiltStep, -TrackPiece.TiltMost, TrackPiece.TiltMost); Say($"The Ramp climbs {tilt:0} over its length."); return; }
        if (selected is { Section.Tilts: true } s)
        {
            Remember();
            var i = pieces.Pieces.IndexOf(s);
            selected = pieces.Pieces[i] = s.TiltedBy(way);
            Changed();
            Say($"The Ramp climbs {selected.Tilt:0} over its length.");
            return;
        }
        Say("Only the Ramp tilts: choose the Ramp, or select one.");
    }

    private void DeleteSelected()
    {
        if (selected is null) return;
        Remember(); pieces.Pieces.Remove(selected); selected = null; Changed();
    }

    private void Remember() { undo.Push(pieces.Pieces.ToList()); }
    private void Undo()
    {
        if (undo.Count == 0) return;
        pieces.Pieces.Clear(); pieces.Pieces.AddRange(undo.Pop()); selected = null; Changed();
    }

    private void LayStarter(string name)
    {
        if (ground is null || board.Width is double.NaN) return;
        var (cx, cz) = Cell(new Point(board.Width / 2, board.Height / 2));
        Remember();
        pieces.Pieces.Clear(); selected = null;
        pieces.Pieces.AddRange(TrackPieceSet.Starter(name, cx - 15, cz - 12, Ground(cx, cz), 0));
        Changed();
        Say($"The {name} is laid in the middle of the island: change it as you like, or save it.");
    }

    private void CloseLap()
    {
        if (pieces.CloseLap(out var why) is not { } closing) { Say(why); return; }
        Remember(); pieces.Pieces.AddRange(closing); Changed();
        Say($"The lap is closed with {string.Join(", ", closing.Select(c => c.Section.Name))}.");
    }

    private List<(double X, double Z, double Y)> problemSpots = new();

    private void Changed()
    {
        checks.Children.Clear();
        var items = pieces.Check();
        problemSpots = items.Where(i => i.Kind == TrackPieceSet.CheckKind.Problem).SelectMany(i => i.Spots).ToList();
        foreach (var item in items)
        {
            var (sign, colour) = item.Kind switch
            {
                TrackPieceSet.CheckKind.Ok => ("✓", Color.FromRgb(0x7C, 0xD9, 0x8A)),
                TrackPieceSet.CheckKind.Note => ("•", Color.FromRgb(0xBF, 0xAF, 0xCB)),
                _ => ("✗", Color.FromRgb(0xFF, 0x70, 0x70)),
            };
            var row = new DockPanel { Margin = new Thickness(0, 1, 0, 1) };
            var mark = new TextBlock { Text = sign, Width = 16, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(colour) };
            DockPanel.SetDock(mark, Dock.Left);
            row.Children.Add(mark);
            row.Children.Add(new TextBlock { Text = item.Text, TextWrapping = TextWrapping.Wrap });
            checks.Children.Add(row);
        }
        Draw();
    }

    // The pieces over the map: the road a grey band with its middle line, a jump's gap pink, a loop blue, the pit lane orange; the open ends
    // yellow rings, the check's problems red rings with a cross; the piece being laid pale; the one selected outlined
    private void Draw()
    {
        overlay.Children.Clear();
        void Piece(TrackPiece p, bool pale, bool picked)
        {
            var line = p.Line();
            for (var i = 1; i < line.Count; i++)
            {
                var kind = line[i].Kind;
                Brush brush = kind switch { SketchKind.Jump => Brushes.HotPink, SketchKind.Loop => Brushes.DeepSkyBlue, SketchKind.Pit => Brushes.Orange, _ => new SolidColorBrush(Color.FromRgb(70, 70, 76)) };
                if (kind == SketchKind.Jump) brush = new SolidColorBrush(Color.FromArgb(120, 255, 105, 180));
                var a = Pixel(line[i - 1].X, line[i - 1].Z); var b = Pixel(line[i].X, line[i].Z);
                overlay.Children.Add(new Line { X1 = a.X, Y1 = a.Y, X2 = b.X, Y2 = b.Y, Stroke = brush, StrokeThickness = 7 * MapScale * pieces.Width, Opacity = pale ? 0.45 : 1, StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round });
            }
            var pts = new PointCollection(line.Select(q => Pixel(q.X, q.Z)));
            overlay.Children.Add(new Polyline { Points = pts, Stroke = picked ? Brushes.Gold : Brushes.White, StrokeThickness = picked ? 3 : 1, StrokeDashArray = pale ? new DoubleCollection { 3, 3 } : null, Opacity = pale ? 0.7 : 0.9 });
        }
        foreach (var p in pieces.Pieces) Piece(p, false, ReferenceEquals(p, selected));
        if (ghost is not null) Piece(ghost, true, false);
        foreach (var e in pieces.OpenEnds().Concat(pieces.OpenStarts()))
        {
            var c = Pixel(e.X, e.Z);
            var ring = new Ellipse { Width = 16, Height = 16, Stroke = Brushes.Gold, StrokeThickness = 2.5 };
            Canvas.SetLeft(ring, c.X - 8); Canvas.SetTop(ring, c.Y - 8);
            overlay.Children.Add(ring);
        }
        foreach (var s in problemSpots)
        {
            var c = Pixel(s.X, s.Z);
            var ring = new Ellipse { Width = 24, Height = 24, Stroke = Brushes.Red, StrokeThickness = 3 };
            Canvas.SetLeft(ring, c.X - 12); Canvas.SetTop(ring, c.Y - 12);
            overlay.Children.Add(ring);
            overlay.Children.Add(new Line { X1 = c.X - 7, Y1 = c.Y - 7, X2 = c.X + 7, Y2 = c.Y + 7, Stroke = Brushes.Red, StrokeThickness = 3 });
            overlay.Children.Add(new Line { X1 = c.X - 7, Y1 = c.Y + 7, X2 = c.X + 7, Y2 = c.Y - 7, Stroke = Brushes.Red, StrokeThickness = 3 });
        }
    }

    // The pieces as a plan, written as a shared track into the player's folder
    private void Save()
    {
        if (island is null || ground is null) return;
        var name = nameBox.Text.Trim();
        if (name.Length == 0) { Say("Give the track a name first."); nameBox.Focus(); return; }
        var result = pieces.MakePlan(ground);
        if (result.Plan is null) { Say("The track can't be saved yet: " + string.Join(" ", result.Problems)); return; }
        try
        {
            var folder = SharedTracks.Folder(installDir);
            Directory.CreateDirectory(folder);
            var plan = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"lbra_track_{Environment.ProcessId}.json");
            File.WriteAllText(plan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            }));
            var safe = new string(name.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            var to = System.IO.Path.Combine(folder, safe + RaceTrackShare.Extension);
            for (var n = 2; File.Exists(to); n++) to = System.IO.Path.Combine(folder, $"{safe}_{n}{RaceTrackShare.Extension}");
            RaceTrackShare.Write(to, name, island, plan);
            File.Delete(plan);
            Saved = to;
            Updater.Log(installDir, $"shared tracks: made in the track maker, saved as {to}");
            DialogResult = true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Say($"The track can't be saved: {e.Message}");
        }
    }
}
