﻿using ELogging;
using ESystem;
using ESystem.Asserting;
using Microsoft.Extensions.Configuration;
using NAudio.Wave;
using RossCarlson.Vatsim.Vpilot.Plugins;
using System.Runtime.CompilerServices;
using VPilotMessageAlert;

namespace VPilotMessageAlert
{
  public class VPilotPlugin : RossCarlson.Vatsim.Vpilot.Plugins.IPlugin
  {
    public string Name => "VPilotMessageAlert";
    private BrokerProxy? brokerProxy;
    private ELogging.Logger logger = null!;
    private static readonly VPilotMessageAlert.Settings.Root settings = null!;
    private VatsimDataProvider vatsimDataProvider = null!;
    private string? connectedCallsign;

    static VPilotPlugin()
    {
      var provider = new ConfigurationManager();
      provider.AddJsonFile("settings.json");

      try
      {
        settings = provider.Get<VPilotMessageAlert.Settings.Root>() ?? throw new ApplicationException("Configuration returned null.");
        RegisterLog();
        Logger.Log(typeof(VPilotPlugin), LogLevel.INFO, "Settings loaded");
      }
      catch (Exception ex)
      {
        VPilotPlugin.settings = GetDefaultSettings();
        RegisterLog();
        Logger.Log(typeof(VPilotPlugin), LogLevel.WARNING, $"Failed to load settings from file 'settings.json'. Reason: {ex.GetFullMessage()}");
      }
      EAssert.IsNotNull(VPilotPlugin.settings);
      if (settings.Events.Count == 0)
        Logger.Log(typeof(VPilotPlugin), LogLevel.WARNING, "No events are monitored. Update configuration file.");

      //TODO add file-exist validation
    }

    private static void RegisterLog()
    {
      Logger.RegisterSenderName(typeof(VPilotPlugin), nameof(VPilotPlugin), false);
      if (System.IO.File.Exists(settings.Logging.FileName))
        try
        {
          System.IO.File.Delete(settings.Logging.FileName);
        }
        catch (Exception)
        {
          // intentionally blank
        }
      Logger.RegisterLogAction(
        li =>
        {
          string s = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {li.Level,-10} {li.SenderName,-20} {li.Message}\n";
          System.IO.File.AppendAllText(settings.Logging.FileName, s);
        },
        new List<LogRule>()
        {
          new(".+", settings.Logging.Level)
        });
    }

    private static VPilotMessageAlert.Settings.Root GetDefaultSettings() => new(new("_log.txt", ELogging.LogLevel.DEBUG));

    public void Initialize(IBroker broker)
    {
      this.logger = ELogging.Logger.Create(this, nameof(VPilotPlugin));
      this.brokerProxy = new(broker);
      PostInitialize();
    }

    public void Initialize(MockBroker broker)
    {
      this.logger = ELogging.Logger.Create(this);
      this.brokerProxy = new(broker);
      PostInitialize();
    }

    public void PostInitialize()
    {
      EAssert.IsNotNull(this.brokerProxy);
      this.brokerProxy.NetworkConnected += Broker_NetworkConnected;
      this.brokerProxy.NetworkDisconnected += Broker_NetworkDisconnected;
      this.brokerProxy.RadioMessageReceived += Broker_RadioMessageReceived;
      this.brokerProxy.SelcalAlertReceived += Broker_SelcalAlertReceived;

      StartVatsimData();
    }

    private void StartVatsimData()
    {
      this.vatsimDataProvider = new(settings.Vatsim);
    }

    private void Broker_SelcalAlertReceived(object? sender, RossCarlson.Vatsim.Vpilot.Plugins.Events.SelcalAlertReceivedEventArgs e)
    {
      logger.Log(LogLevel.INFO, "SelcalAlertReceived");
      var rule = settings.Events.FirstOrDefault(q => q.Action == Settings.EventAction.SelcalAlert);
      logger.Log(LogLevel.DEBUG, rule == null ? "No rule found" : "Found rule with file " + rule.File.Name);
      if (rule != null) TryPlaySound(rule.File);
    }

    private void Broker_RadioMessageReceived(object? sender, RossCarlson.Vatsim.Vpilot.Plugins.Events.RadioMessageReceivedEventArgs e)
    {
      logger.Log(LogLevel.INFO, "RadioMessageReceived");
      var rule = settings.Events.FirstOrDefault(q => q.Action == Settings.EventAction.RadioMessage);
      logger.Log(LogLevel.DEBUG, rule == null ? "No rule found" : "Found rule with file " + rule.File.Name);
      if (rule != null && IsMessageToMonitoredDataMatch(e.Message))
        TryPlaySound(rule.File);
    }

    private bool IsMessageToMonitoredDataMatch(string message)
    {
      bool ret = false;
      if (message.Contains(this.connectedCallsign!))
        ret = true;
      else
      {
        var fd = this.vatsimDataProvider.MonitoredData;
        if (fd != null)
        {
          if (message.Contains(fd.Departure))
            ret = true;
          else if (message.Contains(fd.Arrival))
            ret = true;
          else if (message.Contains(fd.Callsign))
            ret = true;
        }
      }
      this.logger.Log(LogLevel.DEBUG, $"Message '{message}' checked with result {ret}");
      return ret;
    }

    private void Broker_NetworkDisconnected(object? sender, EventArgs e)
    {
      logger.Log(LogLevel.INFO, "NetworkDisconnected");
      var rule = settings.Events.FirstOrDefault(q => q.Action == Settings.EventAction.Disconnected);
      logger.Log(LogLevel.DEBUG, rule == null ? "No rule found" : "Found rule with file " + rule.File.Name);
      if (rule != null) TryPlaySound(rule.File);
    }

    private void Broker_NetworkConnected(object? sender, RossCarlson.Vatsim.Vpilot.Plugins.Events.NetworkConnectedEventArgs e)
    {
      logger.Log(LogLevel.INFO, "NetworkConnected");
      this.connectedCallsign = e.Callsign;

      this.vatsimDataProvider.SetMonitoredVatsimId(e.Cid);
      this.vatsimDataProvider.StartDownloading();

      var rule = settings.Events.FirstOrDefault(q => q.Action == Settings.EventAction.Connected);
      logger.Log(LogLevel.DEBUG, rule == null ? "No rule found" : "Found rule with file " + rule.File.Name);
      if (rule != null) TryPlaySound(rule.File);
    }

    private void TryPlaySound(Settings.File file)
    {
      EAssert.Argument.IsNotNull(file);
      EAssert.Argument.IsTrue(System.IO.File.Exists(file.Name));
      WaveStream mainOutputStream;

      if (file.Name.ToLower().EndsWith(".mp3")) mainOutputStream = new Mp3FileReader(file.Name);
      else if (file.Name.ToLower().EndsWith(".wav")) mainOutputStream = new WaveFileReader(file.Name);
      else
      {
        this.logger.Log(LogLevel.ERROR, $"Unable to play {file.Name}. Only MP3/WAV is supported.");
        return;
      }
      WaveChannel32 volumeStream = new(mainOutputStream);
      WaveOutEvent player = new();
      player.Init(volumeStream);
      player.Play();
    }
  }
}
