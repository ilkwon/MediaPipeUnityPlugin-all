// Copyright (c) 2021 homuler
//
// Use of this source code is governed by an MIT-style
// license that can be found in the LICENSE file or at
// https://opensource.org/licenses/MIT.

// ATTENTION!: This code is for a tutorial and it's broken as is.

using UnityEngine;

namespace Mediapipe.Unity.Tutorial
{
  public class HelloWorld : MonoBehaviour
  {
    private const string configText = @"
    input_stream:""in""
    output_stream:""out"";
    node {
      calculator:""PassThroughCalculator""
            input_stream:""in""
            output_stream:""out1"";
    }
    node {
      calculator:""PassThroughCalculator""
            input_stream:""out1""
            output_stream:""out"";
    }
  ";

    private void Start()
    {
      var graph = new CalculatorGraph(configText);
      var poller = graph.AddOutputStreamPoller<string>("out").Value();
      graph.StartRun().AssertOk();

      for (int i = 0; i < 10; i++)
      {
        graph.AddPacketToInputStream("in", new StringPacket("Hello World", new Timestamp(i))).AssertOk();
      }
      graph.CloseInputStream("in").AssertOk();
      var packet = new StringPacket();
      while (poller.Next(packet))
      {
        Debug.Log(packet.Get());
      }
      graph.WaitUntilDone().AssertOk();
    }


  }
}
