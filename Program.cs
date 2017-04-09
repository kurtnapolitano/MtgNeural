using System;
using Encog.Neural.Networks;
using Encog.Neural.Networks.Layers;
using Encog.Engine.Network.Activation;
using Encog.ML.Data;
using Encog.Neural.Networks.Training.Propagation.Resilient;
using Encog.ML.Train;
using Encog.ML.Data.Basic;
using Encog;

namespace MtgNeural
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            bool retrain = false;
            bool trainMore = false;
            // these actually aren't used until the end.  These are our arbitary validation examples.
            var testValues = new string[][] {
                new[]{ "1", "deathtouch"},
                new[]{ "2", "deathtouch"},
                new[]{ "3", "deathtouch"},
                new[]{ "4", "deathtouch"},
                new[]{ "5", "deathtouch"},
                new[]{ "6", "deathtouch"},
                new[]{ "7", "deathtouch"},
                new[]{ "8", "deathtouch"},
                new[]{ "9", "deathtouch"},
                new[]{ "10","deathtouch"},
            };

            BasicNetwork network;
            if (retrain)
            {
                network = CreateAndTrainNetwork();
            }
            else
            {
                network = LoadNetwork();

                if(trainMore)
                {
                    Train(network);
                }
            }

            TestNetwork(testValues, network);

            EncogFramework.Instance.Shutdown();
            Console.ReadLine();
        }

        private static BasicNetwork LoadNetwork()
        {
            var network = (BasicNetwork)Encog.Util.SerializeObject.Load("network.data");
            return network;
        }

        private static void TestNetwork(string[][] testValues, BasicNetwork network)
        {
            // test the neural network
            Console.WriteLine(@"Neural Network Test Results:");

            foreach (var tv in testValues)

            {
                IMLData output2 = network.Compute(new BasicMLData(MtgDataLoader.GenerateInputs(tv[0], tv[1])));

                double[] outputArray = new double[5];
                for (int i = 0; i < 5; i++)
                {
                    outputArray[i] = output2[i];
                }

                // Shiny pretty console display
                PrintTest(tv, outputArray);
            }
        }

        private static BasicNetwork CreateAndTrainNetwork()
        {
            // create a neural network, without using a factory
            BasicLayer inputLayer, hidden;
            var network = new BasicNetwork();
            network.AddLayer(inputLayer = new BasicLayer(null, true, MtgDataLoader.InputVectorCount));
            network.AddLayer(hidden = new BasicLayer(new ActivationSigmoid(), true, 60)); // MtgDataLoader.InputVectorCount * MtgDataLoader.InputVectorCount));
            network.AddLayer(new BasicLayer(new ActivationSigmoid(), false, 5));
            hidden.ContextFedBy = inputLayer;
            network.Structure.FinalizeStructure();
            network.Reset();
            Train(network);

            return network;
        }

        private static void Train(BasicNetwork network)
        {
            // load all of the cards from the MagicAssistant library
            var cards = MtgDataLoader.GetAllCards(@"C:\Users\napol\MagicAssistantWorkspace\magiccards\MagicDB");

            // build inputs and ideals
            var mtgData = new MtgDataLoader(cards);
            var input = mtgData.Inputs;
            var ideal = mtgData.Ideals;

            // create training data
            IMLDataSet trainingSet = new BasicMLDataSet(input, ideal);

            // train the neural network
            IMLTrain train = new ResilientPropagation(network, trainingSet);

            int epoch = 1;

            do
            {
                train.Iteration();
                if (epoch % 10 == 0)
                {
                    Console.WriteLine(@"Epoch #" + epoch + @" Error:" + train.Error);
                }

                epoch++;

                // if we're not below 1% after 600 iterations (highly unlikely) just bow out.
                if (epoch > 1000)
                {
                    break;

                }

            } while (train.Error > 0.01);

            train.FinishTraining();

            Console.WriteLine($"Final Error: {train.Error}");

            Console.WriteLine("Writing network to file");

            if (System.IO.File.Exists("network.data"))
            {
                System.IO.File.Delete("network.data");
            }

            Encog.Util.SerializeObject.Save("network.data", network);
        }

        private static void PrintTest(string[] tv, double[] outputArray)
        {
            var manaCostString = $"Mana Cost: {tv[0].PadLeft(3, ' ')}";
            var weights = $"W: {outputArray[0]:0.0000}  U: {outputArray[1]:0.0000}, B: {outputArray[2]:0.0000}, R: {outputArray[3]:0.0000}, G: {outputArray[4]:0.0000}";
            var text = tv[1];
            var prediction = MtgDataLoader.ConvertOutputToCardColors(outputArray).PadLeft(5, ' ');
            Console.WriteLine($"{manaCostString} | {text.PadRight(30,' ')} | {prediction.PadRight(5,' ')} | {weights} ");
        }
    }
}
