using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace spacebattle1
{
    public class GameStateDto
    {
        public int PlayerHealth { get; set; }
        public int PlayerScore { get; set; }
        public int Combo { get; set; }
        public double PlayerX { get; set; }
        public double PlayerY { get; set; }
        public bool IsGameOver { get; set; }
        public bool IsPaused { get; set; }
        public List<PositionDto> Asteroids { get; set; }
        public List<PositionDto> Lasers { get; set; }
        public List<PositionDto> Explosions { get; set; }
    }

    public class PositionDto
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Radius { get; set; }
    }

    public partial class MainWindow : Window
    {
        private TcpClient _client;
        private StreamWriter _writer;
        private StreamReader _reader;
        private bool _isConnected = false;
        private Random _bgRand = new Random();

        private int _lastScore = 0;
        private int _lastHealth = 3;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _client = new TcpClient(TxtIp.Text, 8888);
                NetworkStream stream = _client.GetStream();
                _writer = new StreamWriter(stream) { AutoFlush = true };
                _reader = new StreamReader(stream);
                _isConnected = true;

                MenuPanel.Visibility = Visibility.Collapsed;
                GameCanvas.Visibility = Visibility.Visible;

                Thread listenThread = new Thread(ListenServer) { IsBackground = true };
                listenThread.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка зв'язку із сервером: {ex.Message}");
            }
        }

        private void BtnHelp_Click(object sender, RoutedEventArgs e)
        {
            ShowHelpDialog();
        }

        private void ShowHelpDialog()
        {
            MessageBox.Show(
                "Курсова робота з дисципліни 'Основи програмування та алгоритмічні мови'\n" +
                "Тема: Розробка клієнт-серверної гри 'Space Battle'\n" +
                "Розробник: Чаварга Станіслав Анатолійович",
                "Панель інженерної довідки", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ListenServer()
        {
            try
            {
                while (_isConnected)
                {
                    string json = _reader.ReadLine();
                    if (string.IsNullOrEmpty(json)) break;
                    GameStateDto state = JsonSerializer.Deserialize<GameStateDto>(json);
                    Application.Current.Dispatcher.Invoke(() => Render(state));
                }
            }
            catch { }
        }

        private void Render(GameStateDto state)
        {
            if (state == null) return;

            GameCanvas.Children.Clear();

            if (state.IsGameOver)
            {
                _isConnected = false;
                TextBlock txtOver = new TextBlock
                {
                    Text = $"MISSION FAILED\n\nFINAL SCORE: {state.PlayerScore}\n\nНатисніть ESC для виходу",
                    Foreground = Brushes.OrangeRed,
                    FontSize = 36,
                    FontWeight = FontWeights.Bold,
                    TextAlignment = TextAlignment.Center,
                    Width = 800
                };
                Canvas.SetLeft(txtOver, 0); Canvas.SetTop(txtOver, 220);
                GameCanvas.Children.Add(txtOver);
                return;
            }

            if (state.PlayerScore > _lastScore) { Console.Beep(650, 35); _lastScore = state.PlayerScore; }
            if (state.PlayerHealth < _lastHealth) { Console.Beep(140, 200); _lastHealth = state.PlayerHealth; }

            // HUD
            Brush hudBrush = state.PlayerHealth == 1 ? Brushes.Red : Brushes.Gold;
            TextBlock hudText = new TextBlock
            {
                Text = $"HULL INTEGRITY: {state.PlayerHealth * 33}%   |   GALAXY SCORE: {state.PlayerScore}",
                Foreground = hudBrush,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Width = 800
            };
            Canvas.SetLeft(hudText, 0); Canvas.SetTop(hudText, 15);
            GameCanvas.Children.Add(hudText);

            // Зірки
            for (int i = 0; i < 15; i++)
            {
                Ellipse star = new Ellipse { Width = 2, Height = 2, Fill = Brushes.White };
                Canvas.SetLeft(star, (_bgRand.Next(0, 800) - (state.PlayerScore / 10) * 4) % 800);
                Canvas.SetTop(star, _bgRand.Next(0, 600));
                GameCanvas.Children.Add(star);
            }

            // Лазери
            if (state.Lasers != null)
            {
                foreach (var l in state.Lasers)
                {
                    Rectangle laser = new Rectangle { Width = 12, Height = 3, Fill = Brushes.Red };
                    Canvas.SetLeft(laser, l.X - 6); Canvas.SetTop(laser, l.Y - 1.5);
                    GameCanvas.Children.Add(laser);
                }
            }

            // Астероїди
            if (state.Asteroids != null)
            {
                foreach (var a in state.Asteroids)
                {
                    Polygon rock = new Polygon { Fill = Brushes.Gray, Stroke = Brushes.DarkGray, StrokeThickness = 1 };
                    double r = a.Radius;
                    rock.Points = new PointCollection()
                    {
                        new Point(a.X + r, a.Y - r * 0.3),
                        new Point(a.X + r * 0.5, a.Y - r),
                        new Point(a.X - r * 0.5, a.Y - r),
                        new Point(a.X - r, a.Y + r * 0.3),
                        new Point(a.X - r * 0.5, a.Y + r),
                        new Point(a.X + r * 0.5, a.Y + r)
                    };
                    GameCanvas.Children.Add(rock);
                }
            }

            // Вибухи
            if (state.Explosions != null)
            {
                foreach (var exp in state.Explosions)
                {
                    Ellipse wave = new Ellipse { Width = exp.Radius * 2, Height = exp.Radius * 2, Stroke = Brushes.Orange, StrokeThickness = 2 };
                    Canvas.SetLeft(wave, exp.X - exp.Radius); Canvas.SetTop(wave, exp.Y - exp.Radius);
                    GameCanvas.Children.Add(wave);
                }
            }

            // Вогонь двигуна
            double fireSize = _bgRand.Next(12, 22);
            Ellipse fire = new Ellipse { Width = fireSize, Height = 6, Fill = Brushes.OrangeRed };
            Canvas.SetLeft(fire, state.PlayerX - 20 - fireSize); Canvas.SetTop(fire, state.PlayerY - 3);
            GameCanvas.Children.Add(fire);

            // Бойовий корабель
            Polygon ship = new Polygon { Fill = Brushes.SteelBlue, Stroke = Brushes.Cyan, StrokeThickness = 1.5 };
            ship.Points = new PointCollection() {
                new Point(state.PlayerX + 18, state.PlayerY),
                new Point(state.PlayerX - 10, state.PlayerY - 6),
                new Point(state.PlayerX - 18, state.PlayerY - 12),
                new Point(state.PlayerX - 12, state.PlayerY - 3),
                new Point(state.PlayerX - 12, state.PlayerY + 3),
                new Point(state.PlayerX - 18, state.PlayerY + 12),
                new Point(state.PlayerX - 10, state.PlayerY + 6)
            };
            GameCanvas.Children.Add(ship);

            // Кабіна пілота
            Ellipse cockpit = new Ellipse { Width = 8, Height = 4, Fill = Brushes.LightBlue, Stroke = Brushes.White, StrokeThickness = 0.5 };
            Canvas.SetLeft(cockpit, state.PlayerX + 2); Canvas.SetTop(cockpit, state.PlayerY - 2);
            GameCanvas.Children.Add(cockpit);
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.F1) { ShowHelpDialog(); return; }
            if (e.Key == Key.Escape) { this.Close(); return; }

            if (!_isConnected) return;
            string key = e.Key == Key.W ? "W" : e.Key == Key.S ? "S" : e.Key == Key.A ? "A" : e.Key == Key.D ? "D" : e.Key == Key.Space ? "Space" : "";
            if (key != "") _writer.WriteLine($"KEY_DOWN:{key}");
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (!_isConnected) return;
            string key = e.Key == Key.W ? "W" : e.Key == Key.S ? "S" : e.Key == Key.A ? "A" : e.Key == Key.D ? "D" : "";
            if (key != "") _writer.WriteLine($"KEY_UP:{key}");
        }
    }
}