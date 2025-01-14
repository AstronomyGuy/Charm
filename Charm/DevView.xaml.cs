﻿using System.Text;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Field;
using Field.Entities;
using Field.General;
using Field.Models;
using Field.Statics;

namespace Charm;

public partial class DevView : UserControl
{
    private static MainWindow _mainWindow = null;
    private FbxHandler _fbxHandler = null;

    public DevView()
    {
        InitializeComponent();
    }
    
    private void OnControlLoaded(object sender, RoutedEventArgs routedEventArgs)
    {
        _mainWindow = Window.GetWindow(this) as MainWindow;
        _fbxHandler = new FbxHandler(false);
        HashLocation.Text = $"PKG:\nPKG ID:\nEntry Index:";
    }
    
    private void TagHashBoxKeydown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return && e.Key != Key.H && e.Key != Key.R && e.Key != Key.E && e.Key != Key.L)
        {
            return;
        }
        string strHash = TagHashBox.Text.Replace(" ", "");
        strHash = Regex.Replace(strHash, @"(\s+|r|h)", "");
        if (strHash.Length == 16)
        {
            strHash = TagHash64Handler.GetTagHash64(strHash);
        }
        if (strHash == "")
        {
            TagHashBox.Text = "INVALID HASH";
            return;
        }

        TagHash hash;
        if (strHash.Contains("-"))
        {
            var s = strHash.Split("-");
            var pkgid = Int32.Parse(s[0], NumberStyles.HexNumber);
            var entryindex = Int32.Parse(s[1], NumberStyles.HexNumber);
            hash = new TagHash(PackageHandler.MakeHash(pkgid, entryindex));
        }
        else
        {
            hash = new TagHash(strHash);
        }
        
        if (!hash.IsValid())
        {
            TagHashBox.Text = "INVALID HASH";
            return;
        }
        //uint to int
        switch (e.Key)
        {
            case Key.L:
                StringBuilder data = new StringBuilder();
                data.AppendLine($"PKG: {PackageHandler.GetPackageName(hash.GetPkgId())}");
                data.AppendLine($"PKG ID: {hash.GetPkgId()}");
                data.AppendLine($"Entry Index: {hash.GetEntryIndex() }");
                data.AppendLine($"Dev String: {hash.GetDevString() ?? hash.GetContainerString() ?? "NULL"}");
                data.AppendLine($"Reference Hash: {hash.Hash}");

                HashLocation.Text = data.ToString();
                break;
            case Key.Return:
                AddWindow(hash);
                break;
            case Key.H:
                OpenHxD(hash);
                break;
            case Key.R:
                TagHash refHash = PackageHandler.GetEntryReference(hash);
                if (!refHash.GetHashString().EndsWith("8080"))
                {
                    OpenHxD(refHash);
                }
                else
                {
                    TagHashBox.Text = $"REF {refHash}";
                }
                break;
            case Key.E:
                Entity entity = PackageHandler.GetTag(typeof(Entity), hash);
                if (entity.Model != null)
                {
                    OpenHxD(entity.Model.Hash);
                }
                else
                {
                    TagHashBox.Text = $"NO MODEL";
                }
                break;
        }
    }
    
    private void ExportWem(ExportInfo info)
    {
        Wem wem = PackageHandler.GetTag(typeof(Wem), new TagHash(info.Hash));
        string saveDirectory = ConfigHandler.GetExportSavePath() + $"/Sound/{info.Hash}_{info.Name}/";
        Directory.CreateDirectory(saveDirectory);
        wem.SaveToFile($"{saveDirectory}/{info.Name}.wav");
    }

    private void AddWindow(TagHash hash)
    {
        _fbxHandler.Clear();
        // Adds a new tab to the tab control
        DestinyHash reference = PackageHandler.GetEntryReference(hash);
        int hType, hSubtype;
        PackageHandler.GetEntryTypes(hash, out hType, out hSubtype);
        if (hType == 26 && hSubtype == 7)
        {
            var audioView = new TagView();
            audioView.SetViewer(TagView.EViewerType.TagList);
            audioView.MusicPlayer.SetWem(PackageHandler.GetTag(typeof(Wem), hash));
            audioView.MusicPlayer.Play();
            audioView.ExportControl.SetExportFunction(ExportWem, (int)EExportTypeFlag.Full);
            audioView.ExportControl.SetExportInfo(hash);
            _mainWindow.MakeNewTab(hash, audioView);
            _mainWindow.SetNewestTabSelected();
        }
        else if (hType == 32)
        {
            TextureHeader textureHeader = PackageHandler.GetTag(typeof(TextureHeader), new TagHash(hash));
            if (textureHeader.IsCubemap())
            {
                var cubemapView = new CubemapView();
                cubemapView.LoadCubemap(textureHeader);
                _mainWindow.MakeNewTab(hash, cubemapView);
            }
            else
            {
                var textureView = new TextureView();
                textureView.LoadTexture(textureHeader);
                _mainWindow.MakeNewTab(hash, textureView);
            }
            _mainWindow.SetNewestTabSelected();
        }
        else if ((hType == 8 || hType == 16) && hSubtype == 0)
        {
            switch (reference.Hash)
            {
                case 0x80809AD8:
                    EntityView entityView = new EntityView();
                    entityView.LoadEntity(hash, _fbxHandler);
                    _mainWindow.MakeNewTab(hash, entityView);
                    _mainWindow.SetNewestTabSelected();
                    break;
                case 0x80806D44:
                    StaticView staticView = new StaticView();
                    staticView.LoadStatic(hash, ELOD.MostDetail);
                    _mainWindow.MakeNewTab(hash, staticView);
                    _mainWindow.SetNewestTabSelected();
                    break;
                case 0x808093AD:
                    MapView mapView = new MapView();
                    mapView.LoadMap(hash, ELOD.LeastDetail);
                    _mainWindow.MakeNewTab(hash, mapView);
                    _mainWindow.SetNewestTabSelected();
                    break;
                case 0x80808E8E:
                    ActivityView activityView = new ActivityView();
                    activityView.LoadActivity(hash);
                    _mainWindow.MakeNewTab(hash, activityView);
                    _mainWindow.SetNewestTabSelected();
                    break;
                case 0x808099EF:
                    var stringView = new TagView();
                    stringView.SetViewer(TagView.EViewerType.TagList);
                    stringView.TagListControl.LoadContent(ETagListType.Strings, hash, true);
                    _mainWindow.MakeNewTab(hash, stringView);
                    _mainWindow.SetNewestTabSelected();
                    break;
                case 0x808097B8:
                    var dialogueView = new DialogueView();
                    dialogueView.Load(hash);
                    _mainWindow.MakeNewTab(hash, dialogueView);
                    _mainWindow.SetNewestTabSelected();
                    break;
                default:
                    MessageBox.Show("Unknown reference: " + Endian.U32ToString(reference));
                    break;
            }
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    private void OpenHxD(TagHash hash)
    {
        string savePath = ConfigHandler.GetExportSavePath() + "/temp";
        if (!Directory.Exists(savePath))
        {
            Directory.CreateDirectory(savePath);
        }
        string path = $"{savePath}/{hash.GetPkgId().ToString("x4")}_{PackageHandler.GetEntryReference(hash)}_{hash}.bin";
        using (var fileStream = new FileStream(path, FileMode.Create))
        {
            using (var writer = new BinaryWriter(fileStream))
            {
                byte[] data = new DestinyFile(hash).GetData();
                writer.Write(data);
            }
        }
        new Process
        {
            StartInfo = new ProcessStartInfo($@"{path}")
            {
                UseShellExecute = true
            }
        }.Start();
    }

    private void ExportDevMapButton_OnClick(object sender, RoutedEventArgs e)
    {
        // Not actually a map, but a list of assets that are good for testing
        // The assets are assembled in UE5 so just have to rip the list
        var assets = new List<string>()
        {
            "6C24BB80",
            "a237be80",
            "b540be80",
            "68a8b480",
            "fba4b480",
            "e1c5b280",
            "0F3CBE80",
            "A229BE80",
            "B63BBE80",
            "CB32BE80",
        };

        foreach (var asset in assets)
        {
            StaticView.ExportStatic(new TagHash(asset), asset, EExportTypeFlag.Full, "devmap");
        }
    }
}