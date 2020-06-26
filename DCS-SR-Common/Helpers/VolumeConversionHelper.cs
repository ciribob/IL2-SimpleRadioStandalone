using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ciribob.IL2.SimpleRadio.Standalone.Common.Helpers
{
    public static class VolumeConversionHelper
    {


        // 20 / ln( 10 )
        private const double LOG_2_DB = 8.6858896380650365530225783783321;

        // ln( 10 ) / 20
        private const double DB_2_LOG = 0.11512925464970228420089957273422;

        /// <summary>
        /// linear to dB conversion
        /// </summary>
        /// <param name="lin">linear value</param>
        /// <returns>decibel value</returns>
        public static double LinearToDecibels(double lin)
        {
            return Math.Log(lin) * LOG_2_DB;
        }

        /// <summary>
        /// dB to linear conversion
        /// </summary>
        /// <param name="dB">decibel value</param>
        /// <returns>linear value</returns>
        public static double DecibelsToLinear(double dB)
        {
            return Math.Exp(dB * DB_2_LOG);
        }

        public static double ConvertFloatToDB(float linear)
        {
            if (linear == 0)
            {
                //basically nothing but not 0 or we'll get -nan
                linear = 0.0000158f;
            }

            return LinearToDecibels(linear);
        }


        public static string ConvertLinearDiffToDB(float delta)
        {
            float diff = (float) VolumeConversionHelper.ConvertFloatToDB((float) (delta)) -
                         (float) VolumeConversionHelper.ConvertFloatToDB((float) (1.0));

            return Math.Round(diff) + " dB";
            //convert diff into db
        }

        public static float ConvertVolumeSliderToScale(float volume)
        {
            var db = (-30) + (28 - (-30)) * volume;
            return (float) Math.Exp(db / 20 * Math.Log(10));
        }
    }
}