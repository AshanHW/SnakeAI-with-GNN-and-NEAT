using NEAT_GNN;
using NEAT_GNN.Core;
using System.Diagnostics;

namespace SnakeGame
{
    public class SnakeTrainer
    {
        GenomeHandler handler;
        public int Generation { get; private set; }
        public SnakeTrainer()
        {
            handler = new GenomeHandler(
                numOfgenomes: 500,
                numOfInputnodes: 11,
                numOfOutputnodes: 3,
                initMode: WeightInitMode.RandomUniform,
                compatibilityCoeffs: new double[] { 1, 1, 0.4 },
                MutationParams: new double[] { 1.0, 0.001, 0.001 });
        }
        public Genome TrainOneGeneration()
        {
            Dictionary<int, double> fitness = new();

            Genome bestGenome = null;
            double bestFitness = double.MinValue;

            const int MAX_STEPS_PER_FOOD = 75;

            foreach (var IdGenome in handler.Genomes)
            {
                Genome genome = IdGenome.Value;
                var game = new GameState(rows:25, cols:25);
                var agent = new SnakeAIController(genome);

                double f = 0.0;
                int stepsSinceLastFood = 0;
                int foodCount = 0;

                Position food = agent.FindFood(game);

                while (!game.GameOver && stepsSinceLastFood < MAX_STEPS_PER_FOOD)
                {
                    var dir = agent.Decide(game);
                    Position prevHead = game.HeadPosition();
                    game.ChangeDirection(dir);
                    game.Move();

                    stepsSinceLastFood++;
                    // survival reward
                    f += 0.1;

                    Position newHead = game.HeadPosition();
                    Position newFood = agent.FindFood(game);

                    int distBefore = Math.Abs(prevHead.Row - food.Row) + Math.Abs(prevHead.Col - food.Col);
                    int distAfter = Math.Abs(newHead.Row - newFood.Row) + Math.Abs(newHead.Col - newFood.Col);
                    f += (distBefore - distAfter) * 1;

                    if (game.Score > foodCount)
                    {
                        foodCount = game.Score;
                        int stepsToFood = stepsSinceLastFood;
                        stepsSinceLastFood = 0;

                        // Strong reward for eating
                        f += 1000;

                        // Efficiency bonus
                        f += (MAX_STEPS_PER_FOOD - stepsToFood) * 1.0;
                    }
                    food = newFood;
                }
                // Penalty
                if (game.GameOver)
                    f -= 500;
                else if (stepsSinceLastFood >= MAX_STEPS_PER_FOOD)
                    f -= 1000;

                fitness[genome.ID] = f;
                if (f > bestFitness)
                {
                    bestFitness = f;
                    bestGenome = genome;
                }
            }
            handler.EvolveOneGeneration(fitness, 0.2);
            Generation++;

            return bestGenome;
        }
    }
}
