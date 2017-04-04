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
            // these actually aren't used until the end.  These are our arbitary validation examples.
            var testValues = new[] {
                "Trample",
                "Flying trample",
                "Lifelink flying",
                "Haste double strike",
                "dies return gravyard hand",
                "flash evolve",
                "infect trample",
                "bolster flying",
                "hexproof",
                "trample haste",
                "sacrifice flying",
                "draw dies",
                "evolve undying",
                "double strike menace",
                "first strike lifelink graveyard",
                "protection sacrifice flash",
                "prevent bolster hexproof"
            };

            // create a neural network, without using a factory
            BasicLayer inputLayer, hidden, hidden2;
            var network = new BasicNetwork();
            network.AddLayer(inputLayer = new BasicLayer(null, true, MtgDataLoader.InputVectorCount));
            network.AddLayer(hidden = new BasicLayer(new ActivationSigmoid(), true, 18)); // MtgDataLoader.InputVectorCount * MtgDataLoader.InputVectorCount));
            network.AddLayer(new BasicLayer(new ActivationSigmoid(), false, 5));
            hidden.ContextFedBy = inputLayer;
            network.Structure.FinalizeStructure();
            network.Reset();


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
                if(epoch > 600)
                {
                    break;

                }

            } while (train.Error > 0.01);

            train.FinishTraining();

            // test the neural network
            Console.WriteLine(@"Neural Network Results:");
            Console.WriteLine($"Final Error: {train.Error}");

            foreach (var tv in testValues)

            {
                IMLData output2 = network.Compute(new BasicMLData(MtgDataLoader.GenerateInputs(tv)));

                double[] outputArray = new double[5];
                for (int i = 0; i < 5; i++)
                {
                    outputArray[i] = output2[i];
                }

                // Shiny pretty console display
                Console.WriteLine($"{tv.PadLeft(40, ' ')} = :  {MtgDataLoader.ConvertOutputToCardColors(outputArray).PadLeft(5, ' ')} --- W: {outputArray[0]:0.0000}  U: {outputArray[1]:0.0000}, B: {outputArray[2]:0.0000}, R: {outputArray[3]:0.0000}, G: {outputArray[4]:0.0000}");
            }

            EncogFramework.Instance.Shutdown();
            Console.ReadLine();
        }
    }
}
