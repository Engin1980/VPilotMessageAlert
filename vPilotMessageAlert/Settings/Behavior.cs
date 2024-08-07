﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VPilotMessageAlert.Settings
{
  public class Behavior
  {
    public bool SendPrivateMessageWhenConnectedForTheFirstTime { get; set; } = true;
    public bool SendPrivateMessageWhenFlightPlanDetected { get; set; } = true;
    public int RepeatAlertIntervalWhenDisconnected { get; set; } = -1;
  }
}
