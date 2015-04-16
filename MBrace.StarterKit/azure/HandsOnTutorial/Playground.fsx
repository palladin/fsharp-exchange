#load "credentials.fsx"
#load "lib/helpers.fsx"

open System
open System.IO
open MBrace
open MBrace.Azure
open MBrace.Azure.Client
open MBrace.Workflows
open MBrace.Flow



let cluster = Runtime.GetHandle(config)
cluster.AttachClientLogger(ConsoleLogger())



cluster.ShowWorkers()
cluster.ShowProcesses()
cluster.ShowLogs()





let storeClient = cluster.StoreClient
let fileStore = storeClient.FileStore

let files = fileStore.File.Enumerate "wiki" |> Seq.take 1000 |> Seq.toArray








let proc = 
    CloudFlow.ofCloudFilesByLine files
    |> CloudFlow.length
    |> cluster.CreateProcess







let proc' = 
    CloudFlow.ofCloudFilesByLine files
    |> CloudFlow.collect (fun text -> Helpers.splitWords text |> Seq.map Helpers.wordTransform)
    |> CloudFlow.filter Helpers.wordFilter
    |> CloudFlow.countBy id
    |> CloudFlow.sortBy (fun (_, c) -> -c) 20
    |> CloudFlow.toArray
    |> cluster.CreateProcess

proc'.AwaitResult()