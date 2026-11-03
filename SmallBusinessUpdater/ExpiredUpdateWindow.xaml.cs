using System;
using System.Windows;
using System.Windows.Markup;

namespace SmallBusinessUpdater;

public partial class ExpiredUpdateWindow : Window, IComponentConnector
{
	public ExpiredUpdateWindow(DateTime updateEndDate)
	{
		InitializeComponent();
		ExpiredDateTextBlock.Text = $"Okres aktualizacji wygasł {updateEndDate:dd.MM.yyyy}";
	}

	private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = true;
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
	}
}
