using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace MtgNeural
{
    public class MtgDataLoader
    {
        private static List<string> _keywords = new List<string>
        {
            "first strike", "deathtouch", "double strike", "vigilance", "trample",
            "undying", "wither", "flash", "menace", "prowess", "reach", "flying",
            "hexproof", "lifelink", "sacrifice", "dies", "graveyard", "draw", "indestructible",
            "haste", "evolve", "return", "hand", "persist", "infect", "bolster", "protection",
            "prevent"
        };

        private double[][] _inputs;
        private double[][] _ideals;


        public static List<InputCard> GetAllCards(string dbPath)
        {
            var allCards = new List<InputCard>();
            var dir = new DirectoryInfo(dbPath);
            foreach (var file in dir.GetFiles("*.xml", SearchOption.TopDirectoryOnly))
            {
                allCards.AddRange(GetSingleSetOfCards(file.FullName));
            }

            return allCards;
        }

        public static List<InputCard> GetSingleSetOfCards(string xml)
        {
            var result = new List<InputCard>();
            var setName = Path.GetFileNameWithoutExtension(xml);
            if(setName == "Unhinged" || setName == "Unglued")
            {
                return result;
            }

            Console.WriteLine($"Loading : {setName}");

            var serializer = new XmlSerializer(typeof(cards));
            cards serCards;
            using (var fs = new FileStream(xml, FileMode.Open))
            {
                serCards = (cards)serializer.Deserialize(fs);
            }

            foreach (var card in serCards.list)
            {
                if (card.type == null)
                    continue;

                if (!card.type.Contains("Creature"))
                    continue;

                if (card.cost == null)
                    continue;

                if (!"WUBRG".Any(color => card.cost.Contains(color)))
                    continue;

                result.Add(new InputCard
                {
                    Cost = card.cost,
                    RulesText = card.oracleText?.ToLower()
                });
            }

            return result;
        }

        public static int InputVectorCount { get { return _keywords.Count; } }

        public MtgDataLoader(List<InputCard> cards)
        {
            var inputs = new List<double[]>();
            var ideals = new List<double[]>();
            for (int i = 0; i < cards.Count; i++)
            {
                var pair = ConvertCardToPair(cards[i]);
                if (pair == null)
                    continue;

                inputs.Add(pair.input);
                ideals.Add(pair.ideal);
            }

            _inputs = new double[inputs.Count][];
            _ideals = new double[ideals.Count][];

            for (int i =0; i<inputs.Count; i++)
            {
                _inputs[i] = inputs[i];
                _ideals[i] = ideals[i];
            }
        }

        public double[][] Inputs
        {
            get
            {
                return _inputs;
            }
        }

        public double[][] Ideals
        {
            get
            {
                return _ideals;
            }
        }

        public static string ConvertOutputToCardColors(double[] output)
        {
            var result = "";

            if (output[0] > 0.4f) result += "W";
            if (output[1] > 0.4f) result += "U";
            if (output[2] > 0.4f) result += "B";
            if (output[3] > 0.4f) result += "R";
            if (output[4] > 0.4f) result += "G";

            return result;
        }

        private InputOutputPair ConvertCardToPair(InputCard card)
        {
            double[] inputs = GenerateInputs(card.RulesText ?? "");

            if(inputs == null)
            {
                return null;
            }

            double[] ideals = GenerateIdeals(card.Cost ?? "");

            return new InputOutputPair
            {
                input = inputs,
                ideal = ideals
            };
        }

        private static double[] GenerateIdeals(string cardCost)
        {
            return new double[]
                        {
                cardCost.Contains("{W}") ? 1.0 : 0.0,
                cardCost.Contains("{U}") ? 1.0 : 0.0,
                cardCost.Contains("{B}") ? 1.0 : 0.0,
                cardCost.Contains("{R}") ? 1.0 : 0.0,
                cardCost.Contains("{G}") ? 1.0 : 0.0,
                        };
        }

        private static double? GetCMCNuron(string cardCost)
        {
            //{W}{W}{4}
            var splits = cardCost.Replace("{", "").Replace("}", "");

            int cmc = 0;
            foreach(char c in splits)
            {
                if("WUBRG".Contains(c))
                {
                    cmc++;
                    continue;
                }

                int value;
                if(int.TryParse(c.ToString(), out value))
                {
                    cmc += value;
                    continue;
                }
                
                return null;
            }

            if (cmc > 15)
                return null;

            return (double)cmc / 15.0f;
        }

        public static double[] GenerateInputs(string rulesText)
        {
            var result = new double[_keywords.Count];
            for (int i = 0; i < _keywords.Count; i++)
            {
                result[i] = rulesText.ToLower().Contains(_keywords[i]) ? 1.0 : 0.0;
            }

            if(!result.Any(r => r > 0))
            {
                return null;
            }

            return result;
        }

        public class InputCard
        {
            public string RulesText;
            public string Cost;
        }

        private class InputOutputPair
        {
            public double[] input;
            public double[] ideal;
        }
    }
}
