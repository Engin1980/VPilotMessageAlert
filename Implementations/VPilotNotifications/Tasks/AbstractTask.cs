﻿using Eng.VPilotNetCoreModule;
using Eng.VPilotNotifications.Vatsim;
using ESystem.Asserting;
using ESystem.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace Eng.VPilotNotifications.Tasks
{
  internal abstract class AbstractTask
  {
    protected ClientProxyBroker Broker { get; init; }
    protected Logger Logger { get; init; }
    protected VatsimFlightPlanProvider VatsimFlightPlanProvider { get; init; }
    protected ESimWrapper ESimWrapper { get; init; }
    protected bool IsConnected { get; private set; } = false;

    protected void SendSystemPrivateMessage(string message)
    {
      if (!IsConnected)
      {
        Logger.Log(LogLevel.ERROR, "Unable to send private message when not connected. Message: " + message);
        return;
      }

      Logger.Log(LogLevel.DEBUG, "Sending SYSTEM private message: " + message);
      EAssert.IsNotNull(message, nameof(message));
      this.Broker.SendPrivateMessage(Program.SENDER, message);
    }

    protected AbstractTask(TaskInitData data)
    {
      EAssert.IsNotNull(data, nameof(data));
      EAssert.IsNotNull(data.Broker, nameof(data.Broker));
      EAssert.IsNotNull(data.VatsimFlightPlanProvider, nameof(data.VatsimFlightPlanProvider));
      EAssert.IsNotNull(data.ESimWrapper, nameof(data.ESimWrapper));
      EAssert.IsTrue(data.ESimWrapper.Open.IsOpened);

      this.Broker = data.Broker;
      this.VatsimFlightPlanProvider = data.VatsimFlightPlanProvider;
      this.ESimWrapper = data.ESimWrapper;
      this.Logger = Logger.Create(GetType().Name);

      this.Broker.NetworkConnected += (s, e) => this.IsConnected = true;
      this.Broker.NetworkDisconnected += (s, e) => this.IsConnected = false;
    }
  }
}
