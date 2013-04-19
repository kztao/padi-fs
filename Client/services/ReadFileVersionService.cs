﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Remoting.Channels;
using System.Collections;
using System.Runtime.Remoting.Channels.Tcp;
using System.Runtime.Remoting;
using System.Runtime.Serialization.Formatters;
using System.Threading.Tasks;
using CommonTypes;
using CommonTypes.Exceptions;

namespace Client.services
{
    class ReadFileVersionService : ClientService
    {
        private Client Client { get; set; }
        private String FileName { get; set; }
        public int FileVersion { get; set; }

        public ReadFileVersionService(ClientState clientState, String fileName)
            : base(clientState)
        {
            FileName = fileName;
            FileVersion = -1;
        }

        override public void execute()
        {
            Console.WriteLine("#Client: reading async file version for file '" + FileName + "'");

            FileMetadata fileMetadata = State.FileMetadataContainer.getFileMetadata(FileName);
            if (fileMetadata.FileServers.Count < fileMetadata.ReadQuorum)
            {
                Console.WriteLine("Client - trying to read file verison in a quorum of " + fileMetadata.ReadQuorum + ", but we only have " + fileMetadata.FileServers.Count + " in the local metadata ");
                updateWriteFileMetadata(FileName);

            }
            Task<int>[] tasks = new Task<int>[fileMetadata.FileServers.Count];
            for (int ds = 0; ds < fileMetadata.FileServers.Count; ds++)
            {
                tasks[ds] = createAsyncTask(fileMetadata, ds);
            }

            FileVersion = waitReadQuorum(tasks, fileMetadata.WriteQuorum);
        }


        private void updateWriteFileMetadata(String filename)
        {

            Task<FileMetadata>[] tasks = new Task<FileMetadata>[MetaInformationReader.Instance.MetaDataServers.Count];
            for (int md = 0; md < MetaInformationReader.Instance.MetaDataServers.Count; md++)
            {
                IMetaDataServer metadataServer = MetaInformationReader.Instance.MetaDataServers[md].getObject<IMetaDataServer>();
                Console.WriteLine("updateReadFileMetadata [filename: " + filename + ", metadataServer: " + md);
                tasks[md] = Task<FileMetadata>.Factory.StartNew(() => { return metadataServer.updateWriteMetadata(State.Id, filename); });
            }

            Console.WriteLine("updateWriteFileMetadata - waitingQuorum");
            FileMetadata fileMetadata = waitQuorum<FileMetadata>(tasks, 1);
            Console.WriteLine("updateWriteFileMetadata - quorum achieved. Result: " + fileMetadata.FileServers.Count);
            closeUncompletedTasks(tasks);
            int position = State.FileMetadataContainer.addFileMetadata(fileMetadata);
            Console.WriteLine("#Client: metadata saved in position " + position);

        }

        public int waitReadQuorum(Task<int>[] tasks, int quorum)
        {
            List<int> responses = new List<int>();
            while (responses.Count < quorum)
            {
                responses = new List<int>();
                for (int i = 0; i < tasks.Length; ++i)
                {
                    if (tasks[i].IsCompleted)
                    {
                        try
                        {
                            responses.Add(tasks[i].Result);
                        }
                        catch (AggregateException)
                        {
                            FileMetadata fileMetadata = State.FileMetadataContainer.getFileMetadata(FileName);
                            tasks[i] = createAsyncTask(fileMetadata, i);
                        }
                    }
                }
            }

            closeUncompletedTasks(tasks);
            //choose the bettew option
            return findMax(responses);
        }

        private int findMax(List<int> responses)
        {
            int moreRecentVersion = -1;
            foreach (int fileVersion in responses)
            {
                if (fileVersion > moreRecentVersion)
                {
                    moreRecentVersion = fileVersion;
                }
            }
            return moreRecentVersion;
        }


        private Task<int> createAsyncTask(FileMetadata fileMetadata, int ds)
        {
            IDataServer dataServer = fileMetadata.FileServers[ds].getObject<IDataServer>();
            return Task<int>.Factory.StartNew(() =>
            {
                /*try
                {
                    return dataServer.readFileVersion(FileName);
                }
                catch (Exception) { return 0; }
                }*/

                return dataServer.readFileVersion(FileName);
            }
            );
        }


    }
}
