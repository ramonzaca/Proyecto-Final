using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpNeat.Core;
using SharpNeat.Genomes.Neat;
using SharpNeat.Phenomes;

namespace SharpNeat.Domains.EasyChange
{
    public class EasyChangePredictor
    {
        string _genomePath;
        string _datasetPath;
        string _predictionPath;
        IGenomeDecoder<NeatGenome, IBlackBox> _decoder;
        bool _statusCompleted;

        public EasyChangePredictor(IGenomeDecoder<NeatGenome, IBlackBox> Decoder) {
            _decoder = Decoder;
            _statusCompleted = false;
        }

        public bool StatusCompleted
        {
            get { return _statusCompleted; }
            set { _statusCompleted = value; }
        }

        public string GenomePath
        {
            get { return _genomePath; }
            set { _genomePath = value; }
        }

        public string DatasetPath
        {
            get { return _datasetPath; }
            set { _datasetPath = value; }
        }

        public string PredictionPath
        {
            get { return _predictionPath; }
            set { _predictionPath = value; }
        }

        public int Predict(NeatGenome genome, double[] inputs)
        {
            double output;
            IBlackBox box = _decoder.Decode(genome);
            ISignalArray inputArr = box.InputSignalArray;
            ISignalArray outputArr = box.OutputSignalArray;

            for (int j = 0; j < inputs.Length; j++)
            {
                inputArr[j] = inputs[j];
            }

            // Activate the black box.
            box.Activate();
            if (!box.IsStateValid)
            {   // Any black box that gets itself into an invalid state is unlikely to be
                // any good, so let's just exit here.
                return -1;
            }

            // Read output signal.
            output = outputArr[0];

            // Reset black box state ready for next test case.
            box.ResetState();


            if (output >= 0.5)
                return 1;
            else
                return 0;
        }


    }
}
