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
    class WriteFileService : ClientService
    {
        public File NewFile { get; set; }

        public WriteFileService(ClientState clientState, File file)
            : base(clientState)
        {
            NewFile = file;
        }

        override public void execute()
        {
            if (NewFile == null || NewFile.Content == null || NewFile.FileName == null)
            {
                throw new WriteFileException("Client - trying to write null file " + NewFile);
            }

            if (!State.FileMetadataContainer.containsFileMetadata(NewFile.FileName))
            {
                throw new WriteFileException("Client - tryng to write a file that is not in the file-register");
            }

            FileMetadata fileMetadata = State.FileMetadataContainer.getFileMetadata(NewFile.FileName);
            if (!fileMetadata.IsOpen)
            {
                throw new WriteFileException("Client - tryng to write a file that is not open");
            }
            if (fileMetadata.FileServers.Count < fileMetadata.WriteQuorum)
            {
                Console.WriteLine("Client - trying to write in a quorum of " + fileMetadata.WriteQuorum + ", but we only have " + fileMetadata.FileServers.Count + " in the local metadata ");
                updateWriteFileMetadata(NewFile.FileName);
                fileMetadata = State.FileMetadataContainer.getFileMetadata(NewFile.FileName);
            }

            Console.WriteLine("#Client: writing file '" + NewFile.FileName + "' with content: '" + NewFile.Content + "', as string: " + System.Text.Encoding.UTF8.GetString(NewFile.Content));

            Task[] tasks = new Task[fileMetadata.FileServers.Count];
            for (int ds = 0; ds < fileMetadata.FileServers.Count; ds++)
            {
                tasks[ds] = createAsyncWriteTask(fileMetadata, ds);
            }

            waitWriteQuorum(tasks, fileMetadata.WriteQuorum);
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

        public void waitWriteQuorum(Task[] tasks, int quorum)
        {
            int responsesCounter = 0;
            while (responsesCounter < quorum)
            {
                responsesCounter = 0;
                for (int i = 0; i < tasks.Length; ++i)
                {
                    if(tasks[i].IsCompleted){

                        if (tasks[i].Exception != null)
                        {
                            //in case the write gives an error we resend the message until we get a quorum
                            FileMetadata fileMetadata = State.FileMetadataContainer.getFileMetadata(NewFile.FileName);
                            tasks[i] = createAsyncWriteTask(fileMetadata, i);
                        }
                        else {
                            responsesCounter++;
                        }
                    }
                }
            }
            closeUncompletedTasks(tasks);

        }



        private Task createAsyncWriteTask(FileMetadata fileMetadata, int ds)
        {
            IDataServer dataServer = fileMetadata.FileServers[ds].getObject<IDataServer>();
            return Task.Factory.StartNew(() =>
            {
                /*try
                {
                    dataServer.write(NewFile);
                }
                catch (Exception) { }*/
                dataServer.write(NewFile);
            });
        
        }
        
    }
}
