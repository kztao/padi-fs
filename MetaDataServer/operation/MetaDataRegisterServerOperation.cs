﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonTypes;

namespace MetaDataServer
{
    [Serializable]
    class MetaDataRegisterServerOperation : MetaDataOperation
    {
        String DataServerId { get; set; }
        String DataServerHost { get; set; }
        int DataServerPort { get; set; }

        public MetaDataRegisterServerOperation(String dataServerId, String dataServerHost, int dataServerPort)
        {
            DataServerId = dataServerId;
            DataServerHost = dataServerHost;
            DataServerPort = dataServerPort;
        }

         public override void execute(MetaDataServer md)
        {
            
            Console.WriteLine("#MDS: Registering DS " + DataServerId);
            if ( md.DataServers.ContainsKey(DataServerId) ) 
            {
                md.DataServers.Remove(DataServerId);
            }
            ServerObjectWrapper remoteObjectWrapper = new ServerObjectWrapper(DataServerPort, DataServerId, DataServerHost);
            md.DataServers.Add(DataServerId, remoteObjectWrapper);
            md.addServerToUnbalancedFiles(DataServerId);

            HeartbeatMessage heartbeat = new HeartbeatMessage(DataServerId, 0 , 0, 0, 0);
            md.receiveHeartbeat(heartbeat);
            md.makeCheckpoint();
        }      
    }
}
