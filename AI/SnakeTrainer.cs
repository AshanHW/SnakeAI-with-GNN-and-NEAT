using NEAT_GNN;
using NEAT_GNN.Core;
using System.Diagnostics;

namespace SnakeGame
{
    public class SnakeTrainer
    {
        public GenomeHandler handler;
        public int Generation { get; private set; }
        public int maxStepsFood;
        public double CompatibilityThreshold;
        public double deathPenalty;
        public double distanceReward;
        public double foodReward;
        public double effReward;

        public SnakeTrainer(int populationSize, double[] coeffs, double[] mutationParams, int maxStepsFood, double compThreshold,
            double deathPenalty, double distanceReward, double foodReward, double effReward)
        {
            handler = new GenomeHandler(
                numOfgenomes: populationSize,
                numOfInputnodes: 11,
                numOfOutputnodes: 3,
                initMode: WeightInitMode.RandomUniform,
                compatibilityCoeffs: coeffs,
                MutationParams: mutationParams);

            this.maxStepsFood = maxStepsFood;
            this.CompatibilityThreshold = compThreshold;

            this.deathPenalty = deathPenalty;
            this.distanceReward = distanceReward;
            this.foodReward = foodReward;
            this.effReward = effReward;

        }
        public Genome TrainOneGeneration()
        {
            Dictionary<int, double> fitness = new();

            Genome bestGenome = null;
            double bestFitness = double.MinValue;

            foreach (var IdGenome in handler.Genomes)
            {
                Genome genome = IdGenome.Value;
                var game = new GameState(rows:25, cols:25);
                var agent = new SnakeAIController(genome);

                double f = 0.0;
                int stepsSinceLastFood = 0;
                int foodCount = 0;

                Position food = agent.FindFood(game);

                while (!game.GameOver && stepsSinceLastFood < maxStepsFood)
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
                    // Progress towards food reward
                    f += (distBefore - distAfter) * distanceReward;

                    if (game.Score > foodCount)
                    {
                        foodCount = game.Score;
                        int stepsToFood = stepsSinceLastFood;
                        stepsSinceLastFood = 0;

                        // Strong reward for eating
                        f += foodReward;

                        // Efficiency bonus
                        f += (maxStepsFood - stepsToFood) * effReward;
                    }
                    food = newFood;
                }
                // Penalty
                if (game.GameOver || stepsSinceLastFood >= maxStepsFood)
                { 
                    f -= deathPenalty;
                };

                fitness[genome.ID] = f;
                if (f > bestFitness)
                {
                    bestFitness = f;
                    bestGenome = genome;
                }
            }
            handler.EvolveOneGeneration(fitness, CompatibilityThreshold);
            Generation++;

            return bestGenome;
        }
    }
}
