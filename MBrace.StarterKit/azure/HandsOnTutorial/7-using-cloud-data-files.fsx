﻿#load "credentials.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Workflows
open MBrace.Flow

(**
 This tutorial illustrates creating and using cloud files, and then processing them using cloud streams.
 
 Before running, edit credentials.fsx to enter your connection strings.
**)

// First connect to the cluster
let cluster = Runtime.GetHandle(config)

cluster.ShowProcesses()
cluster.ShowWorkers()

// Here's some data that simulates a log file for user click events
let linesOfFile = 
    [ for i in 1 .. 1000 do 
         let time = DateTime.Now.Date.AddSeconds(float i)
         let text = sprintf "click user%d %s" (i%10) (time.ToString())
         yield text ]

// Upload the data to a cloud file (held in blob storage). A fresh name is generated 
// for the could file.
let anonCloudFile = 
     cloud { 
         let! file = CloudFile.WriteAllLines linesOfFile
         return file 
     }
     |> cluster.Run

// Run a cloud job which reads all the lines of a cloud file.

let numberOfLinesInFile = 
    cloud { 
        let! data = CloudFile.ReadAllLines anonCloudFile
        return data.Length 
    }
    |> cluster.Run

// Get all the directories in the cloud file system
let directories = cluster.StoreClient.FileStore.Directory.Enumerate()

// Create a directory in the cloud file system
let freshDirectory = cluster.StoreClient.FileStore.Directory.Create()

// By default, CloudFile.WriteAllLines uses a fresh random file name
// in the "user data" directory.  Below, you give an exact name to the 
// file.
//
// Upload data to a cloud file (held in blob storage) where we give the cloud file a name.
let namedCloudFile = 
    cloud { 
        let fileName = freshDirectory.Path + "/file1"
        do! CloudFile.Delete(fileName) 
        let! file = CloudFile.WriteAllLines(linesOfFile, path = fileName) 
        return file
    } 
    |> cluster.Run

// Read the named cloud file as part of a cloud job
let numberOfLinesInNamedFile = 
    cloud { 
        let! data = CloudFile.ReadAllLines namedCloudFile 
        return data.Length 
    }
    |> cluster.Run

cluster.ShowLogs(240.0)

(** 

Now we generate a collection of cloud files and process them using cloud streams.

**)

// Generate 100 cloud files in the cloud storage
let namedCloudFilesJob = 
    [ for i in 1 .. 100 ->
        // Note that we generate the contents of the files in the cloud - this cloud
        // computation below only captures and sends an integer.
        cloud { 
            let lines = [for j in 1 .. 100 -> "File " + string i + ", Item " + string (i * 100 + j) + ", " + string (j + i * 100) ] 
            let nm = freshDirectory.Path + "/file" + string i
            do! CloudFile.Delete(path=nm) 
            let! file = CloudFile.WriteAllLines(lines,path=nm) 
            return file 
        } ]
   |> Cloud.Parallel 
   |> cluster.CreateProcess

// Check progress
namedCloudFilesJob.ShowInfo()

// Get the result
let namedCloudFiles = namedCloudFilesJob.AwaitResult()

// A collection of cloud files can be used as input to a cloud
// parallel data flow. This is a very powerful feature.
let sumOfLengthsOfLinesJob =
    namedCloudFiles 
    |> CloudFlow.ofCloudFiles CloudFileReader.ReadAllLines
    |> CloudFlow.map (fun lines -> lines.Length)
    |> CloudFlow.sum
    |> cluster.CreateProcess

// Check progress
sumOfLengthsOfLinesJob.ShowInfo()

// Get the result
let sumOfLengthsOfLines = sumOfLengthsOfLinesJob.AwaitResult()


