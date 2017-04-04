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
        // list of words we care about in the oracle text
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

        /// <summary>
        /// Loads all cards from Magic Assistant xmls
        /// </summary>
        /// <param name="dbPath">Path to MagicAssistant's workspace MagicDB folder.  
        /// By default this is: C:\Users\[username]\MagicAssistantWorkspace\magiccards\MagicDB
        /// </param>
        public static List<InputCard> GetAllCards(string dbPath)
        {
            var allCards = new List<InputCard>();
            var dir = new DirectoryInfo(dbPath);
            foreach (var file in dir.GetFiles("*.xml", SearchOption.TopDirectoryOnly))
            {
                // ignore Unhinged and Unglued always
                if (file.Name.Contains("Unhinged") || file.Name.Contains("Unglued"))
                {
                    continue;
                }

                allCards.AddRange(GetSingleSetOfCards(file.FullName));
            }

            return allCards;
        }

        /// <summary>
        /// Loads a list of cards from a single Magic assistant XML
        /// </summary>
        public static List<InputCard> GetSingleSetOfCards(string xmlPath)
        {
            var result = new List<InputCard>();
            var setName = Path.GetFileNameWithoutExtension(xmlPath);
            
            Console.WriteLine($"Loading : {setName}");

            var serializer = new XmlSerializer(typeof(cards));
            cards serCards;
            using (var fs = new FileStream(xmlPath, FileMode.Open))
            {
                serCards = (cards)serializer.Deserialize(fs);
            }

            foreach (var card in serCards.list)
            {
                // card must have a type and it must be of type creature
                if (card.type == null)
                    continue;
                if (!card.type.Contains("Creature"))
                    continue;

                // card must have a cost (no lands) and it must contain at least one color
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

            // so this pretty much sucks, because we have lower level data filters, some of these could come back as null
            // and i'm an idiot so we're doing two loops here, one to collect the data and then once we know how many we 
            // have, we can initalize the arrays to the correct size and populate them.  Bad code is bad but it's working.
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

        // We can convert output data into a color string based on whether each output neuron exceeds our set threshold.

        private static double _colorValidationThreshold = 0.4;
        public static string ConvertOutputToCardColors(double[] output)
        {
            var result = "";

            if (output[0] > _colorValidationThreshold) result += "W";
            if (output[1] > _colorValidationThreshold) result += "U";
            if (output[2] > _colorValidationThreshold) result += "B";
            if (output[3] > _colorValidationThreshold) result += "R";
            if (output[4] > _colorValidationThreshold) result += "G";

            return result;
        }

        /// <summary>
        /// Translate cardspeak into double arrays so that the network can do the needful with it.
        /// </summary>
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

        /// <summary>
        /// Create the idea output for a given cost -- in neural net speak (so an array of doubles)
        /// </summary>
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

        /// <summary>
        /// Use the card cost to create a converted mana cost neuron -- this method is currently unused
        /// </summary>
        private static double? GetCMCNuron(string cardCost)
        {
            //example: {4}{W}{W}

            // strip out the brackets
            var splits = cardCost.Replace("{", "").Replace("}", "");

            int cmc = 0;
            foreach(char c in splits)
            {
                // each color letter is 1 mana
                if("WUBRG".Contains(c))
                {
                    cmc++;
                    continue;
                }

                // each numeric symbol is its value
                int value;
                if(int.TryParse(c.ToString(), out value))
                {
                    cmc += value;
                    continue;
                }
                

                // anything else in the cost is considered an invalid character, call this card a wash and skip it
                // This will happen with things like hybrid mana represented as {W/G}.  I'm too lazy to write a parser.
                return null;
            }

            // there are stupid unhinged and unglued cards, they'll mess everythign up.  Nothing over 15.
            if (cmc > 15)
                return null;

            // technically we should be normalizing over -1.0,1.0 but seeing as how all the other input neurons
            // are either 0 or 1, we'll likely be using a sigmoid activation function which doesn't go into negatives
            // so lets just stay away from there.  Okay?
            return (double)cmc / 15.0f;
        }

        /// <summary>
        /// Parse the oracle text for any keywords and light up those neurons
        /// </summary>
        public static double[] GenerateInputs(string rulesText)
        {
            var result = new double[_keywords.Count];
            for (int i = 0; i < _keywords.Count; i++)
            {
                result[i] = rulesText.ToLower().Contains(_keywords[i]) ? 1.0 : 0.0;
            }

            // if the card is a dud, like a vanilla card or something stupid that doesn't do anything useful like
            // Search the city.  Just throw it out, we aren't dealing with that stuff.
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
