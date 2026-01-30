using NEAT_GNN;
using NEAT_GNN.Core;
using System.Linq;
using System.Numerics;

namespace SnakeGame
{
    public class SnakeAIController
    {
        private readonly Genome genome;

        public SnakeAIController(Genome genome)
        {
            this.genome = genome;
        }

        public Direction Decide(GameState game)
        {
            double[] inputs = EncodeState(game);
            double[] outputs = genome.Forward(inputs);

            int action = ArgMax(outputs);

            return ActionToDirection(action, game.Dir);
        }

        private int ArgMax(double[] v)
        {
            int idx = 0;
            double max = v[0];
            for (int i = 1; i < v.Length; i++)
            {
                if (v[i] > max)
                {
                    max = v[i];
                    idx = i;
                }
            }
            return idx;
        }

        private double[] EncodeState(GameState state)
        {
            Position head = state.HeadPosition();
            Position food = FindFood(state);

            List<double> inputs = new List<double>();

            // 1) Direction one-hot
            inputs.Add(state.Dir == Direction.Up ? 1 : 0);
            inputs.Add(state.Dir == Direction.Right ? 1 : 0);
            inputs.Add(state.Dir == Direction.Down ? 1 : 0);
            inputs.Add(state.Dir == Direction.Left ? 1 : 0);

            // 2) Food relative position
            //inputs.Add((food.Col - head.Col) / (double)state.Cols);
            //inputs.Add((food.Row - head.Row) / (double)state.Rows);

            // 2) Food direction one-hot (absolute)
            inputs.Add(food.Col < head.Col ? 1 : 0); // food left
            inputs.Add(food.Col > head.Col ? 1 : 0); // food right
            inputs.Add(food.Row < head.Row ? 1 : 0); // food up
            inputs.Add(food.Row > head.Row ? 1 : 0); // food down

            // 3) Danger sensors
            Direction forward = state.Dir;
            Direction leftDir;
            Direction rightDir;

            if (state.Dir == Direction.Up)
            {
                leftDir = Direction.Left;
                rightDir = Direction.Right;
            }
            else if (state.Dir == Direction.Down)
            {
                leftDir = Direction.Right;
                rightDir = Direction.Left;
            }
            else if (state.Dir == Direction.Left)
            {
                leftDir = Direction.Down;
                rightDir = Direction.Up;
            }
            else // Right
            {
                leftDir = Direction.Up;
                rightDir = Direction.Down;
            }

            inputs.Add(IsDanger(state, head.Translate(forward)) ? 1 : 0);   // forward
            inputs.Add(IsDanger(state, head.Translate(leftDir)) ? 1 : 0);   // left
            inputs.Add(IsDanger(state, head.Translate(rightDir)) ? 1 : 0);  // right

            return inputs.ToArray();
        }

        private bool IsDanger(GameState state, Position pos)
        {
            // Bound check
            if (pos.Row < 0 || pos.Row >= state.Rows || pos.Col < 0 || pos.Col >= state.Cols)
                return true;

            var val = state.Grid[pos.Row, pos.Col];
            return val == GridValue.Snake || val == GridValue.Outside;
        }

        public Position FindFood(GameState game)
        {
            for (int r = 0; r < game.Rows; r++)
                for (int c = 0; c < game.Cols; c++)
                    if (game.Grid[r, c] == GridValue.Food)
                        return new Position(r, c);

            return new Position(0, 0);
        }

        private Direction ActionToDirection(int action, Direction current)
        {
            if (action == 1)
                return current; // straight

            if (action == 0) // turn left
            {
                if (current == Direction.Up) return Direction.Left;
                if (current == Direction.Left) return Direction.Down;
                if (current == Direction.Down) return Direction.Right;
                if (current == Direction.Right) return Direction.Up;
            }

            if (action == 2) // turn right
            {
                if (current == Direction.Up) return Direction.Right;
                if (current == Direction.Right) return Direction.Down;
                if (current == Direction.Down) return Direction.Left;
                if (current == Direction.Left) return Direction.Up;
            }

            return current; // fallback safety
        }

    }
}
