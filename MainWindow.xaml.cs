using Microsoft.Win32;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ModelPeek;

public partial class MainWindow : Window
{
    private const double MinPitch = -Math.PI * 0.47;
    private const double MaxPitch = Math.PI * 0.49;

    private readonly ModelVisual3D _modelVisual = new();
    private readonly string? _startupFile;

    private Point _lastMousePosition;
    private DragMode _dragMode;
    private Point3D _target = new(0, 0, 0);
    private double _yaw = -Math.PI / 4;
    private double _pitch = Math.PI / 6;
    private double _distance = 100;
    private double _modelRadius = 50;
    private bool _modelLoaded;

    public MainWindow(string? startupFile)
    {
        InitializeComponent();
        _startupFile = startupFile;

        AddLights();
        Viewport.Children.Add(_modelVisual);

        Loaded += MainWindow_Loaded;
        SizeChanged += (_, _) =>
        {
            if (_modelLoaded)
            {
                UpdateCamera();
            }
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_startupFile is not null)
        {
            await OpenModelAsync(_startupFile);
        }
        else
        {
            UpdateCamera();
        }
    }

    private void AddLights()
    {
        var lights = new Model3DGroup();
        lights.Children.Add(new AmbientLight(Color.FromRgb(78, 80, 86)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(245, 243, 238), new Vector3D(-1, 1, -1.4)));
        lights.Children.Add(new DirectionalLight(Color.FromRgb(111, 119, 132), new Vector3D(1, -0.4, -0.2)));
        Viewport.Children.Add(new ModelVisual3D { Content = lights });
    }

    private async Task OpenModelAsync(string path)
    {
        if (!string.Equals(Path.GetExtension(path), ".stl", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                this,
                "Model Peek currently opens STL files only.",
                "Unsupported file",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        try
        {
            LoadingText.Text = $"Opening {Path.GetFileName(path)}…";
            LoadingOverlay.Visibility = Visibility.Visible;

            StlModel model = await Task.Run(() => StlLoader.Load(path));
            var material = CreateModelMaterial();
            var geometryModel = new GeometryModel3D(model.Geometry, material)
            {
                BackMaterial = material
            };
            geometryModel.Freeze();

            _modelVisual.Content = geometryModel;
            _modelRadius = Math.Max(model.Radius, 0.01);
            _modelLoaded = true;

            FileNameText.Text = Path.GetFileName(path);
            FileInfoText.Text = $"{model.TriangleCount:N0} triangles  •  Read-only";
            Title = $"{Path.GetFileName(path)} — Model Peek";
            EmptyState.Visibility = Visibility.Collapsed;
            SetViewButtonsEnabled(true);
            SetIsometricView();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                this,
                $"Model Peek could not open this STL.\n\n{ex.Message}",
                "Couldn’t open model",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private static Material CreateModelMaterial()
    {
        var material = new MaterialGroup();
        var orange = new SolidColorBrush(Color.FromRgb(255, 106, 43));
        var highlight = new SolidColorBrush(Color.FromRgb(255, 185, 140));
        orange.Freeze();
        highlight.Freeze();

        material.Children.Add(new DiffuseMaterial(orange));
        material.Children.Add(new SpecularMaterial(highlight, 28));
        material.Freeze();
        return material;
    }

    private void SetViewButtonsEnabled(bool enabled)
    {
        FitButton.IsEnabled = enabled;
        IsoButton.IsEnabled = enabled;
        FrontButton.IsEnabled = enabled;
        TopButton.IsEnabled = enabled;
        RightButton.IsEnabled = enabled;
    }

    private void FitView()
    {
        _target = new Point3D(0, 0, 0);
        double verticalFov = Camera.FieldOfView * Math.PI / 180.0;
        double fitDistance = _modelRadius / Math.Tan(verticalFov / 2.0);
        _distance = Math.Max(fitDistance * 1.22, 0.1);
        UpdateCamera();
    }

    private void SetIsometricView()
    {
        _yaw = -Math.PI / 4;
        _pitch = Math.PI / 6;
        FitView();
    }

    private void SetFrontView()
    {
        _yaw = -Math.PI / 2;
        _pitch = 0;
        FitView();
    }

    private void SetTopView()
    {
        _yaw = -Math.PI / 2;
        _pitch = Math.PI / 2 - 0.001;
        FitView();
    }

    private void SetRightView()
    {
        _yaw = 0;
        _pitch = 0;
        FitView();
    }

    private void UpdateCamera()
    {
        double cosPitch = Math.Cos(_pitch);
        var offset = new Vector3D(
            _distance * cosPitch * Math.Cos(_yaw),
            _distance * cosPitch * Math.Sin(_yaw),
            _distance * Math.Sin(_pitch));

        Camera.Position = _target + offset;
        Camera.LookDirection = -offset;

        var look = Camera.LookDirection;
        look.Normalize();
        var worldUp = new Vector3D(0, 0, 1);
        var right = Vector3D.CrossProduct(look, worldUp);
        if (right.LengthSquared < 0.000001)
        {
            Camera.UpDirection = new Vector3D(0, 1, 0);
        }
        else
        {
            right.Normalize();
            var screenUp = Vector3D.CrossProduct(right, look);
            screenUp.Normalize();
            Camera.UpDirection = screenUp;
        }

        Camera.NearPlaneDistance = Math.Max(_distance / 1000, 0.001);
        Camera.FarPlaneDistance = Math.Max(_distance * 1000, 10000);
    }

    private void Pan(Point current)
    {
        Vector delta = current - _lastMousePosition;
        var look = Camera.LookDirection;
        look.Normalize();
        var right = Vector3D.CrossProduct(look, Camera.UpDirection);
        right.Normalize();
        var up = Camera.UpDirection;
        up.Normalize();

        double scale = _distance * 0.0017;
        _target += right * (-delta.X * scale);
        _target += up * (delta.Y * scale);
        UpdateCamera();
    }

    private void Orbit(Point current)
    {
        Vector delta = current - _lastMousePosition;
        _yaw -= delta.X * 0.008;
        _pitch = Math.Clamp(_pitch + delta.Y * 0.008, MinPitch, MaxPitch);
        UpdateCamera();
    }

    private async void OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open an STL model",
            Filter = "STL models (*.stl)|*.stl",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            await OpenModelAsync(dialog.FileName);
        }
    }

    private void FitButton_Click(object sender, RoutedEventArgs e) => FitView();
    private void IsoButton_Click(object sender, RoutedEventArgs e) => SetIsometricView();
    private void FrontButton_Click(object sender, RoutedEventArgs e) => SetFrontView();
    private void TopButton_Click(object sender, RoutedEventArgs e) => SetTopView();
    private void RightButton_Click(object sender, RoutedEventArgs e) => SetRightView();

    private void Viewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_modelLoaded)
        {
            return;
        }

        _dragMode = DragMode.Orbit;
        BeginDrag(e);
    }

    private void Viewer_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_modelLoaded)
        {
            return;
        }

        _dragMode = DragMode.Pan;
        BeginDrag(e);
    }

    private void Viewer_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!_modelLoaded || e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        _dragMode = DragMode.Pan;
        BeginDrag(e);
    }

    private void BeginDrag(MouseButtonEventArgs e)
    {
        _lastMousePosition = e.GetPosition(ViewerSurface);
        ViewerSurface.CaptureMouse();
        e.Handled = true;
    }

    private void Viewer_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragMode == DragMode.None || !_modelLoaded)
        {
            return;
        }

        Point current = e.GetPosition(ViewerSurface);
        if (_dragMode == DragMode.Orbit)
        {
            Orbit(current);
        }
        else
        {
            Pan(current);
        }

        _lastMousePosition = current;
    }

    private void Viewer_MouseUp(object sender, MouseButtonEventArgs e) => EndDrag();
    private void Viewer_MouseLeave(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Released &&
            e.RightButton == MouseButtonState.Released &&
            e.MiddleButton == MouseButtonState.Released)
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        _dragMode = DragMode.None;
        if (Mouse.Captured == ViewerSurface)
        {
            ViewerSurface.ReleaseMouseCapture();
        }
    }

    private void Viewer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (!_modelLoaded)
        {
            return;
        }

        double zoomFactor = Math.Exp(-e.Delta / 120.0 * 0.14);
        _distance = Math.Clamp(_distance * zoomFactor, _modelRadius * 0.02, _modelRadius * 500);
        UpdateCamera();
        e.Handled = true;
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedStl(e.Data) is not null ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        string? path = GetDroppedStl(e.Data);
        if (path is not null)
        {
            await OpenModelAsync(path);
        }
    }

    private static string? GetDroppedStl(IDataObject data)
    {
        if (!data.GetDataPresent(DataFormats.FileDrop))
        {
            return null;
        }

        return (data.GetData(DataFormats.FileDrop) as string[])
            ?.FirstOrDefault(path =>
                File.Exists(path) &&
                string.Equals(Path.GetExtension(path), ".stl", StringComparison.OrdinalIgnoreCase));
    }

    private async void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.O)
        {
            OpenButton_Click(sender, e);
            e.Handled = true;
            return;
        }

        if (!_modelLoaded)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.F:
                FitView();
                break;
            case Key.D1:
            case Key.NumPad1:
                SetIsometricView();
                break;
            case Key.D2:
            case Key.NumPad2:
                SetFrontView();
                break;
            case Key.D3:
            case Key.NumPad3:
                SetTopView();
                break;
            case Key.D4:
            case Key.NumPad4:
                SetRightView();
                break;
            default:
                return;
        }

        e.Handled = true;
        await Task.CompletedTask;
    }

    private enum DragMode
    {
        None,
        Orbit,
        Pan
    }
}
