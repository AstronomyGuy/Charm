using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using Field;

namespace Charm;

public partial class BlenderConfigView : UserControl
{
    public BlenderConfigView()
    {
        InitializeComponent();
    }
    
    public void OnControlLoaded(object sender, RoutedEventArgs e)
    {
        PopulateConfigPanel();
    }

    private void PopulateConfigPanel()
    {
        BlenderConfigPanel.Children.Clear();

        TextBlock header = new TextBlock();
        header.Text = "Blender Settings";
        header.FontSize = 30;
        BlenderConfigPanel.Children.Add(header);

        TextBlock lbl = new TextBlock();
        lbl.Text = "WORK IN PROGRESS";
        lbl.FontSize = 10;
        BlenderConfigPanel.Children.Add(lbl);
        
        // Enable source 2 shader generation
        ConfigSettingControl cbe = new ConfigSettingControl();
        cbe.SettingName = "Generate shaders (osl)";
        bool bval2 = ConfigHandler.GetBlenderInteropEnabled();
        cbe.SettingValue = bval2.ToString();
        cbe.ChangeButton.Click += BlenderShaderExportEnabled_OnClick;
        BlenderConfigPanel.Children.Add(cbe);
    }

    private void BlenderShaderExportEnabled_OnClick(object sender, RoutedEventArgs e)
    {
        ConfigHandler.SetBlenderInteropEnabled(!ConfigHandler.GetBlenderInteropEnabled());
        PopulateConfigPanel();
    }
}