// A simple program to download (Ri)creazioni al calcolatore past issues 
// from the archive site. I hope it's self-explanatory.

module Ricrcal

open System
open Hopac
open Logary
open Logary.Message
open Logary.Configuration
open Logary.Targets

let genIssues year = 
    let offset = (year - 1987) * 12 + 220
    seq { for x in 1..12 do 
            let n = x + offset
            if n > 0 then yield year, n }

let [<Literal>] Prefix = "https://download.kataweb.it/mediaweb/pdf/espresso/scienze/"
let issueUri (year, number) = sprintf "%s%d_%03d_M.pdf" Prefix year number |> Uri

let issuesBetween yearFrom yearTo = seq { yearFrom..yearTo} |> Seq.collect genIssues 

let fetch (logger: Logger) (issue: Uri) = 
    let wc = new Net.WebClient()
    try
        wc.DownloadFile(issue, IO.Path.GetFileName(issue.AbsolutePath))
        sprintf "downloaded %s" issue.AbsoluteUri 
        |> eventX 
        |> logger.info
    with 
        | :? System.Net.WebException as ex -> if ex.Status = Net.WebExceptionStatus.ProtocolError then
                                               sprintf "protocol error for %s, not downloaded" issue.AbsoluteUri 
                                               |> eventX 
                                               |> logger.warn

[<EntryPoint>]
let main argv =
    use mre = new System.Threading.ManualResetEventSlim(false)
    use sub = Console.CancelKeyPress.Subscribe (fun _ -> mre.Set())

    let logary = 
        Config.create "ricrcal" (System.Net.Dns.GetHostName())
        |> Config.target (LiterateConsole.create LiterateConsole.empty "console")
        |> Config.ilogger (ILogger.Console Debug)
        |> Config.build
        |> run
    
    let logger = logary.getLogger "main"
    let fetchIt = fetch logger
    let downloadJobs = issuesBetween 1968 2010
                        |> Seq.map issueUri
                        |> Array.ofSeq
                        |> Array.map (fun issue -> async { fetchIt issue })

    Async.Parallel (downloadJobs, 5)
    |> Async.RunSynchronously
    |> ignore

    "done!" |> eventX |> logger.info

    mre.Wait()
    0 // return an integer exit code