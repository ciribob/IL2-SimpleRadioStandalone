using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace Ciribob.IL2.SimpleRadio.Standalone.Client.Utils.ValueConverters
{
    class MicAvailabilityTooltipConverter : IValueConverter
    {
		private static ToolTip _noMicAvailable = BuildTooltip();

		public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			bool micAvailable = (bool)value;
			if (micAvailable)
			{
				return null;
			}
			else
			{
				return _noMicAvailable;
			}
		}

		public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
		{
			throw new NotImplementedException();
		}

		private static ToolTip BuildTooltip()
		{
			var NoMicAvailable = new ToolTip();
			StackPanel noMicAvailableContent = new StackPanel();

			noMicAvailableContent.Children.Add(new TextBlock
			{
				Text = "No microphone available",
				FontWeight = FontWeights.Bold
			});
			noMicAvailableContent.Children.Add(new TextBlock
			{
				Text = "No valid microphone is available - others will not be able to hear you."
			});
			noMicAvailableContent.Children.Add(new TextBlock
			{
				Text = "You can still use SRS to listen to radio calls, but will not be able to transmit anything yourself."
			});

			NoMicAvailable.Content = noMicAvailableContent;
			return NoMicAvailable;
		}
	}
}
