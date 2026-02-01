using NEAT_GNN.Core;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using static SnakeGame.Config;
using Path = System.IO.Path;

namespace SnakeGame
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<GridValue, ImageSource> gridValToImage = new()
        {
            {GridValue.Empty, Images.Empty },
            {GridValue.Snake, Images.Body },
            {GridValue.Food, Images.Food }
        };
        private readonly Dictionary<Direction, int> dirToRotation = new()
        {
            {Direction.Up, 0 },
            {Direction.Right, 90 },
            {Direction.Down, 180 },
            {Direction.Left, 270 }
        };

        private readonly AppConfig Config;
        private readonly int rows = 25, cols = 25;
        private readonly Image[,] GridImages;
        private GameState gameState;
        private bool gameRunning;

        private SnakeTrainer trainer;
        private SnakeAIController aiController;
        private bool aiMode = false;

        private readonly List<double> ScoreHistory = new();
        private int bestEverScore = int.MinValue;
        private const string BEST_GENOME_PATH = "Checkpoints/best_genome.json";

        public MainWindow()
        {
            Config = ConfigLoader.Load();
            InitializeComponent();
            GridImages = SetupGrid();
            gameState = new GameState(Config.Game.Rows, Config.Game.Cols);
            double[] coeffsParams = { Config.Neat.C1, Config.Neat.C2, Config.Neat.C3};
            double[] mutationParams = { Config.Neat.WeightMutationRate, Config.Neat.AddConnectionRate, Config.Neat.AddNodeRate};
            trainer = new SnakeTrainer(Config.AIParameters.PopulationSize, coeffsParams, mutationParams, 
                Config.Game.MaxStepsPerFood, Config.AIParameters.CompatibilityThreshold,
                Config.Fitness.DeathPenalty, Config.Fitness.DistanceReward, Config.Fitness.FoodReward, Config.Fitness.EfficiencyBonus);
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.T)   // press T to train
            {
                aiMode = true;
                Overlay.Visibility = Visibility.Visible;
                OverlayText.Text = "TRAINING...";

                await Task.Run(async () =>
                {
                    for (int gen = 0; gen < Config.AIParameters.Generations; gen++)
                    {
                        var best = trainer.TrainOneGeneration(); // All genomes play headless
                        Debug.WriteLine($"Gen {gen} | Best fitness: {best.RawFitness}");

                        // Visualize the best genome on the UI thread
                        
                        await Dispatcher.InvokeAsync(() =>
                        {
                            aiController = new SnakeAIController(best);
                            gameState = new GameState(rows, cols);
                            Overlay.Visibility = Visibility.Visible;
                            OverlayText.Text = $"GEN {gen}";
                            DrawGenome(best);
                        });
                        await Task.Delay(100);

                        await RunAIGame();
                        await Dispatcher.InvokeAsync(() =>
                        {
                            int score = gameState.Score;
                            ScoreHistory.Add(score);
                            DrawScoreGraph();

                            if (score > bestEverScore)
                            {
                                bestEverScore = score;

                                string folder = Path.GetDirectoryName(BEST_GENOME_PATH);
                                if (!Directory.Exists(folder))
                                    Directory.CreateDirectory(folder);

                                trainer.handler.SaveGenome(best, gen, BEST_GENOME_PATH);

                                Debug.WriteLine($"New BEST genome! Gen {gen}, Score {score}");
                                bestEverScore = score;
                            }
                            Overlay.Visibility = Visibility.Hidden;

                        });
                        await Task.Delay(100);
                    }

                    // Done with training
                    await Dispatcher.InvokeAsync(() =>
                    {
                        Overlay.Visibility = Visibility.Visible;
                        OverlayText.Text = "TRAINING COMPLETE! PRESS ANY KEY TO START";
                    });
                });

                return;

            }

            if (e.Key == Key.I)
            {
                if (!File.Exists(BEST_GENOME_PATH))
                {
                    Overlay.Visibility = Visibility.Visible;
                    OverlayText.Text = "NO SAVED GENOME";
                    return;
                }

                var genome = trainer.handler.LoadGenomeFromFile(BEST_GENOME_PATH);
                aiController = new SnakeAIController(genome);
                gameState = new GameState(rows, cols);

                Overlay.Visibility = Visibility.Hidden;
                DrawGenome(genome);

                await RunAIGame();
            }

            if (Overlay.Visibility == Visibility.Visible)
            {
                e.Handled = true;
            }
            if (!gameRunning)
            {
                gameRunning = true;
                await RunGame();
                gameRunning = false;
            }     
        }

        private async Task RunGame()
        {
            Draw();
            await ShowCountDown();
            Overlay.Visibility = Visibility.Hidden;
            await GameLoop();
            await ShowGameOver();
            gameState = new GameState(rows, cols);
        }

        private async Task RunAIGame()
        {
            await Dispatcher.InvokeAsync(() =>
            {
                Overlay.Visibility = Visibility.Hidden;
                Draw();
            });

            // inference best genome
            int steps = 0;
            int current_score = gameState.Score;
            while (!gameState.GameOver && steps<75)
            {
                var dir = aiController.Decide(gameState);
                await Dispatcher.InvokeAsync(() =>
                {
                    gameState.ChangeDirection(dir);
                    gameState.Move();
                    Draw();
                    if (gameState.Score > current_score)
                    {
                        steps = 0;
                    }
                    else
                    {
                        steps++;
                    }
                    current_score = gameState.Score;
                });
                await Task.Delay(Config.AIParameters.VisualSpeed);
            }

            await Dispatcher.InvokeAsync(async () =>
            {
                await DrawDeadSnake();
            });
            await Task.Delay(50);
        }

        private async void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (gameState.GameOver)
            {
                return;
            }
            switch (e.Key)
            {
                case Key.Left:
                    gameState.ChangeDirection(Direction.Left);
                    break;
                case Key.Right:
                    gameState.ChangeDirection(Direction.Right);
                    break;
                case Key.Up:
                    gameState.ChangeDirection(Direction.Up);
                    break;
                case Key.Down:
                    gameState.ChangeDirection(Direction.Down);
                    break;
            }
        }

        private async Task GameLoop()
        {
            while (!gameState.GameOver)
            {
                await Task.Delay(100);
                gameState.Move();
                Draw();
            }
        }

        private Image[,] SetupGrid()
        {
            Image[,] images = new Image[rows, cols];
            GameGrid.Rows = rows;
            GameGrid.Columns = cols;
            GameGrid.Width = GameGrid.Height * (cols / (double)rows);

            for (int r =0; r < rows; r++)
            {
                for (int c =0; c < cols; c++)
                {
                    Image image = new Image
                    {
                        Source = Images.Empty,
                        RenderTransformOrigin = new Point(0.5, 0.5)
                    };
                    images[r, c] = image;
                    GameGrid.Children.Add(image);
                }
            }
            return images;
        }

        private void Draw()
        {
            DrawGrid();
            DrawSnakeHead();
            ScoreText.Text = $"SCORE {gameState.Score}";
        }

        private void DrawGrid()
        {
            for (int r=0; r < rows; r++)
            {
                for(int c=0; c < cols; c++)
                {
                    GridValue gridVal = gameState.Grid[r, c];
                    GridImages[r, c].Source = gridValToImage[gridVal];
                    GridImages[r, c].RenderTransform = Transform.Identity;
                }
            }
        }

        private async Task ShowCountDown()
        {
            for (int i = 3; i>= 1;  i--)
            {
                OverlayText.Text = i.ToString();
                await Task.Delay(500);
            }
        }

        private async Task ShowGameOver()
        {
            DrawDeadSnake();
            await Task.Delay(1000);
            Overlay.Visibility = Visibility.Visible;
            OverlayText.Text = "PRESS ANY KEY TO START";
        }

        private void DrawSnakeHead()
        {
            Position headPos = gameState.HeadPosition();
            Image image = GridImages[headPos.Row, headPos.Col];
            image.Source = Images.Head;

            int rotation = dirToRotation[gameState.Dir];
            image.RenderTransform = new RotateTransform(rotation);
        }

        private async Task DrawDeadSnake()
        {
            List<Position> positions = new List<Position>(gameState.SnakePositions());
            for (int i = 0; i < positions.Count; i++)
            {
                Position pos = positions[i];
                ImageSource source = (i == 0) ? Images.DeadHead : Images.DeadBody;
                GridImages[pos.Row, pos.Col].Source = source;
                await Task.Delay(50);
            }
        }

        private void DrawGenome(Genome genome)
        {
            GenomeCanvas.Children.Clear();

            if (genome == null) return;

            double canvasWidth = GenomeCanvas.Width;
            double canvasHeight = GenomeCanvas.Height;

            // Group nodes by layer
            var layers = genome.Nodes.Values
                .GroupBy(n => n.Layer)
                .OrderBy(g => g.Key)
                .ToList();

            Dictionary<int, Point> nodePositions = new();

            double xStep = canvasWidth / (layers.Count + 1);

            for (int l = 0; l < layers.Count; l++)
            {
                var layer = layers[l].ToList();
                double yStep = canvasHeight / (layer.Count + 1);

                for (int i = 0; i < layer.Count; i++)
                {
                    var node = layer[i];

                    double x = (l + 1) * xStep;
                    double y = (i + 1) * yStep;

                    nodePositions[node.Id] = new Point(x, y);
                }
            }

            // Draw connections FIRST (so nodes appear on top)
            foreach (var c in genome.Connections)
            {
                if (!c.Enabled) continue;

                Point p1 = nodePositions[c.InNode.Id];
                Point p2 = nodePositions[c.OutNode.Id];

                Line line = new Line
                {
                    X1 = p1.X,
                    Y1 = p1.Y,
                    X2 = p2.X,
                    Y2 = p2.Y,
                    Stroke = c.Weight >= 0 ? Brushes.LimeGreen : Brushes.IndianRed,
                    StrokeThickness = Math.Max(1, Math.Abs(c.Weight) * 2),
                    Opacity = 0.8
                };

                GenomeCanvas.Children.Add(line);
            }

            // Draw nodes
            foreach (var kv in nodePositions)
            {
                Ellipse node = new Ellipse
                {
                    Width = 14,
                    Height = 14,
                    Fill = Brushes.White,
                    Stroke = Brushes.Black,
                    StrokeThickness = 1
                };

                Canvas.SetLeft(node, kv.Value.X - 7);
                Canvas.SetTop(node, kv.Value.Y - 7);

                GenomeCanvas.Children.Add(node);
            }
        }

        private void DrawScoreGraph()
        {
            ScoreCanvas.Children.Clear();
            if (ScoreHistory.Count < 2) return;

            double w = ScoreCanvas.ActualWidth;
            double h = ScoreCanvas.ActualHeight;
            if (w <= 2 || h <= 2) return;

            double maxScore = Math.Max(1, ScoreHistory.Max());

            const int LEFT_PAD = 35;
            const int TOP_PAD = 10;
            const int BOTTOM_PAD = 15;

            double plotW = w - LEFT_PAD;
            double plotH = h - TOP_PAD - BOTTOM_PAD;

            Line yAxis = new Line
            {
                X1 = LEFT_PAD,
                Y1 = TOP_PAD,
                X2 = LEFT_PAD,
                Y2 = TOP_PAD + plotH,
                Stroke = Brushes.Gray,
                StrokeThickness = 1
            };
            ScoreCanvas.Children.Add(yAxis);

            int ticks = 4;
            for (int i = 0; i <= ticks; i++)
            {
                double t = i / (double)ticks;
                double y = TOP_PAD + plotH - t * plotH;
                double scoreVal = t * maxScore;

                Line grid = new Line
                {
                    X1 = LEFT_PAD,
                    X2 = w,
                    Y1 = y,
                    Y2 = y,
                    Stroke = Brushes.DimGray,
                    StrokeThickness = 0.5,
                    Opacity = 0.4
                };
                ScoreCanvas.Children.Add(grid);

                TextBlock label = new TextBlock
                {
                    Text = ((int)scoreVal).ToString(),
                    Foreground = Brushes.LightGray,
                    FontSize = 10
                };

                Canvas.SetLeft(label, 2);
                Canvas.SetTop(label, y - 8);
                ScoreCanvas.Children.Add(label);
            }

            Polyline line = new Polyline
            {
                Stroke = Brushes.LimeGreen,
                StrokeThickness = 2
            };

            for (int i = 0; i < ScoreHistory.Count; i++)
            {
                double x = LEFT_PAD + (i / (double)(ScoreHistory.Count - 1)) * plotW;
                double y = TOP_PAD + plotH
                         - (ScoreHistory[i] / maxScore) * plotH;

                line.Points.Add(new Point(x, y));
            }

            ScoreCanvas.Children.Add(line);
        }
    }
}