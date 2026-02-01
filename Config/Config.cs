using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SnakeGame
{
    public class Config
    {
        public class AppConfig
        {
            public GameConfig Game { get; set; }
            public AIParameters AIParameters { get; set; }
            public FitnessConfig Fitness { get; set; }
            public NeatConfig Neat { get; set; }
        }

        public class GameConfig
        {
            public int Rows { get; set; }
            public int Cols { get; set; }
            public int MaxStepsPerFood { get; set; }
        }

        public class AIParameters
        {
            public int Generations { get; set; }
            public int PopulationSize { get; set; }
            public double CompatibilityThreshold { get; set; }
            public int VisualSpeed {  get; set; }
        }

        public class FitnessConfig
        {
            public double FoodReward { get; set; }
            public double DistanceReward { get; set; }
            public double DeathPenalty { get; set; }
            public double EfficiencyBonus { get; set; }
        }

        public class NeatConfig
        {
            public double C1 { get; set; }
            public double C2 { get; set; }
            public double C3 { get; set; }

            public double WeightMutationRate { get; set; }
            public double AddConnectionRate { get; set; }
            public double AddNodeRate { get; set; }
        }
    }
}
