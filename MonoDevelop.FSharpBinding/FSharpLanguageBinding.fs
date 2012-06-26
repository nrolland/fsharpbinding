namespace FSharp.MonoDevelop

open System
open System.Xml
open System.CodeDom.Compiler
open System.IO

open MonoDevelop.Core
open MonoDevelop.Ide
open MonoDevelop.Ide.Gui.Content
open MonoDevelop.Projects
open Microsoft.FSharp.Compiler

//open ICSharpCode.NRefactory.TypeSystem
//open ICSharpCode.NRefactory.Editor
//open ICSharpCode.NRefactory.Completion

type FSharpLanguageBinding() =
  static let LanguageName = "F#"

  let provider = lazy new CodeDom.FSharpCodeProvider()
  
  // ------------------------------------------------------------------------------------
  // When the current document is F# file, we create a timer that allows us to run 
  // a full background type-checking regularly (this recompiles the whole project)
  
  let mutable timerHandle = 0u
  
  /// Runs every 'ServiceSettings.idleTimeout' when current file is F# file
  let idleTimer() = 
    Debug.tracef "Gui" "OnIdle called"
    let doc = IdeApp.Workbench.ActiveDocument
    if doc <> null then
      // Trigger full parse using the current configuration
      let config = IdeApp.Workspace.ActiveConfiguration
      Debug.tracef "Parsing" "Triggering full parse from OnIdle"
      LanguageService.Service.TriggerParse(doc.FileName, doc.Editor.Text, doc.Window.Document, config, full=true)
    true

  // Create or remove Idle timer 
  let createIdleTimer() = 
    timerHandle <- GLib.Timeout.Add(uint32 ServiceSettings.idleTimeout, (fun () -> idleTimer()))
  let removeIdleTimer() = 
    if timerHandle <> 0u then GLib.Source.Remove(timerHandle) |> ignore
    timerHandle <- 0u
  
  // Register handler that will enable/disable timer when F# file is opened/closed
  do IdeApp.Workbench.ActiveDocumentChanged.Add(fun _ ->
    let doc = IdeApp.Workbench.ActiveDocument
    removeIdleTimer()
    if doc <> null && (Common.supportedExtension(IO.Path.GetExtension(doc.FileName.ToString()))) then
      createIdleTimer() )
  
  
  // ------------------------------------------------------------------------------------
  
  interface IDotNetLanguageBinding 
           with
    member x.BlockCommentEndTag = "*)"
    member x.BlockCommentStartTag = "(*"
    member x.Language = LanguageName
    member x.SingleLineCommentTag = "//"
    //member x.Parser = null
    //member x.Refactorer = null

    member x.GetFileName(baseName) = new FilePath(baseName.ToString() + ".fs")
    member x.IsSourceCodeFile(fileName) = StringComparer.OrdinalIgnoreCase.Equals (Path.GetExtension (fileName.ToString()), ".fs")
    
    // IDotNetLanguageBinding
    override x.Compile(items, config, configSel, monitor) : BuildResult =
      CompilerService.Compile(items, config, configSel, monitor)

    override x.CreateCompilationParameters(options:XmlElement) : ConfigurationParameters =
      // Debug.tracef "Config" "Creating compiler configuration parameters"
      new FSharpCompilerParameters() :> ConfigurationParameters

    override x.CreateProjectParameters(options:XmlElement) : ProjectParameters =
      new FSharpProjectParameters() :> ProjectParameters
      
    override x.GetCodeDomProvider() : CodeDomProvider =
      null 
      // TODO: Simplify CodeDom provider to generate reasonable template
      // files at least for some MonoDevelop project types. Then we can recover:
      //   provider.Value :> CodeDomProvider
      
    override x.GetSupportedClrVersions() =
      [| ClrVersion.Net_2_0; ClrVersion.Net_4_0 |]

    override x.ProjectStockIcon = "md-fs-project"