using System.Diagnostics;
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

        private readonly int rows = 25, cols = 25;
        private readonly Image[,] GridImages;
        private GameState gameState;
        private bool gameRunning;

        private SnakeTrainer trainer;
        private SnakeAIController aiController;
        private bool aiMode = false;

        public MainWindow()
        {
            InitializeComponent();
            GridImages = SetupGrid();
            gameState = new GameState(rows, cols);
            trainer = new SnakeTrainer();
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
                    for (int gen = 0; gen < 1000; gen++)
                    {
                        var best = trainer.TrainOneGeneration(); // All genomes play headless
                        Debug.WriteLine($"Gen {gen} | Best fitness: {best.RawFitness}");

                        // Visualize the best genome on the UI thread
                        await Dispatcher.InvokeAsync(async () =>
                        {
                            aiController = new SnakeAIController(best);
                            gameState = new GameState(rows, cols);
                            Overlay.Visibility = Visibility.Hidden;

                            // Run the best genome visually
                            await RunAIGame();
                            Overlay.Visibility = Visibility.Visible;
                            OverlayText.Text = $"GEN {gen}";
                        });
                        await Task.Delay(800);
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
            Draw();
            Overlay.Visibility = Visibility.Hidden;
            // inference best genome
            while (!gameState.GameOver)
            {
                var dir = aiController.Decide(gameState);
                gameState.ChangeDirection(dir);
                await Task.Delay(200);
                gameState.Move();
                Draw();
            }

            //await ShowGameOver();

            await DrawDeadSnake();
            await Task.Delay(200);
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
    }
}