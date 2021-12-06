using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Vitech.Genesys.Common;
using Vitech.Genesys.Client;

namespace package_export
{
    class Program
    {
        static ClientModel client;
        static Repository repository;
        static Boolean downloadComplete;

        static void ChangeNotifier_JobPropertyChanged(object sender, 
            Vitech.Genesys.Client.Coordination.PropertyChangeEventArgs<IJob> e)

        {
            if (e.Updated.Status == Vitech.Genesys.Common.JobStatus.Finished &&
                e.Updated.Type == Vitech.Genesys.Common.JobType.Export &&
                !string.IsNullOrEmpty(e.Updated.SaveToUri) && downloadComplete == false)
            {
                Console.WriteLine("Starting Job Download.....");
                DownloadExportedFile(e.Updated.Id, e.Updated.SaveToUri);
                downloadComplete = true;
                Console.WriteLine("Download Complete!");
            }
        }
        private static void DownloadExportedFile(Guid jobId, String filename)
        {
            IJobResult result = repository.GetJobResult(jobId);
            if (result.ExportFileId.HasValue)
            {
                const int BUFFER_SIZE = 2048;
                byte[] buffer = new byte[BUFFER_SIZE];

                using (Stream exportStream = repository.DownloadFile(result.ExportFileId.Value))
                using (FileStream fileStream = new FileStream(filename, FileMode.Create))
                {
                    int bytesRead = exportStream.Read(buffer, 0, BUFFER_SIZE);
                    while (bytesRead != 0)
                    {
                        fileStream.Write(buffer, 0, bytesRead);
                        bytesRead = exportStream.Read(buffer, 0, BUFFER_SIZE);
                    }
                }
            }
        }

        static void Main(string[] args)
        {
            String projectName;
            int numPackages;

            SynchronizationContext.SetSynchronizationContext(new SynchronizationContext());

            if (args.Length >= 2)
            {
                projectName = args[0];
                numPackages = args.Length - 1;

                client = new ClientModel();
                RepositoryConfiguration repositoryConfiguration =
                    client.GetKnownRepositories().LocalRepository;
                GenesysClientCredentials credentials =
                    new GenesysClientCredentials("api-user", "api-pwd", AuthenticationType.GENESYS);
                repositoryConfiguration.Login(credentials);
                Console.WriteLine("Logged In!");

                repository = repositoryConfiguration.GetRepository();
                repository.ChangeNotifier.JobPropertyChanged += new 
                    EventHandler<Vitech.Genesys.Client.Coordination.PropertyChangeEventArgs<IJob>>(ChangeNotifier_JobPropertyChanged);
                repository.GetJobs();
                repository.ReceiveUpdates(true);

                // setup export structure
                IExportRequest export = repository.GetExportRequest();
                export.SaveToUri = @"file:\\..\..\..\output\export.gnsx";
                export.Database = true;
                export.Comments = "Package Export";
                export.Folders = true;

                IProject project = repository.GetProject(projectName);
                Console.WriteLine("Project Id: " + project.Id);

                // add projects to export
                Guid[] projectList = new Guid[1];
                projectList[0] = project.Id;
                export.ProjectIds = projectList;

                // add package guids to export list
                Guid[] guidList = new Guid[numPackages];
                for (int i=0; i<numPackages; i++)
                {
                    guidList[i] = project.GetFolder("Package").GetEntity(args[i + 1]).Id;
                }
                export.PackageIds.Add(project.Id, guidList);

                downloadComplete = false;
                export.Export();
                while (downloadComplete == false)
                {
                    Thread.Sleep(500);
                }

                client.Dispose();
                System.Environment.Exit(0);
            }
            else
            {
                Console.WriteLine("Usage: <project name> <package name> <package name> <...>");
                System.Environment.Exit(-1);

            }
        }
    }
}
