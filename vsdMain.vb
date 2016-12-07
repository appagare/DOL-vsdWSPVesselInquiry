'TODO:  unit test

Imports System.ServiceProcess
Imports System.Threading
Imports System.Web

Public Class svrMain
    Inherits System.ServiceProcess.ServiceBase

    'module level parameters
    Private _MaxThreads As Integer = 20 'max concurrent threads
    Private _ThreadCount As Integer = 0 'number of threads spawned
    Private _QueueTxName As String = "PRIVATE$\DBQTx" 'name of the common output queue
    Private _QueueRxName As String = "PRIVATE$\Vessel" 'name of the input queue
    Private _QueueServer As String = "DOLUTOLYVSDEV1" 'queue server path name
    Private _QueueSleepWhenEmpty As Integer = 500 'number of milliseconds to wait when the queue is empty before checking again
    Private _DebugMode As Byte = 0
    Private _QueueTxObject As WA.DOL.VSD.WSPQueue.QueueObject 'common output queue object
    Private _LogEventObject As New WA.DOL.LogEvent.LogEvent()

    Private _Proxy As String = ""
    Private _User As String = ""
    Private _Password As String = ""
    Private _Domain As String = ""
    Private _UseSystemCredentials As Boolean = False
    Private _PreAuthenticate As Boolean = False


    Private Enum ServiceStates
        Shutdown = 0
        Paused = 1
        Running = 2
    End Enum

    Private _ServiceState As ServiceStates = ServiceStates.Paused

#Region " Component Designer generated code "

    Public Sub New()
        MyBase.New()

        ' This call is required by the Component Designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call

    End Sub

    'UserService overrides dispose to clean up the component list.
    Protected Overloads Overrides Sub Dispose(ByVal disposing As Boolean)
        If disposing Then
            If Not (components Is Nothing) Then
                components.Dispose()
            End If
        End If
        MyBase.Dispose(disposing)
    End Sub

    ' The main entry point for the process
    <MTAThread()> _
    Shared Sub Main()
        Dim ServicesToRun() As System.ServiceProcess.ServiceBase

        ' More than one NT Service may run within the same process. To add
        ' another service to this process, change the following line to
        ' create a second service object. For example,
        '
        '   ServicesToRun = New System.ServiceProcess.ServiceBase () {New Service1, New MySecondUserService}
        '
        ServicesToRun = New System.ServiceProcess.ServiceBase() {New svrMain()}

        System.ServiceProcess.ServiceBase.Run(ServicesToRun)
    End Sub

    'Required by the Component Designer
    Private components As System.ComponentModel.IContainer

    ' NOTE: The following procedure is required by the Component Designer
    ' It can be modified using the Component Designer.  
    ' Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> Private Sub InitializeComponent()
        '
        'svrMain
        '
        Me.CanShutdown = True
        Me.ServiceName = "WSP Vessel Inquiry"

    End Sub

#End Region

    Protected Overrides Sub OnStart(ByVal args() As String)
        ' Add code here to start your service. This method should set things
        ' in motion so your service can do its work.

        Try
            'read the app settings
            _ReadAppSettings()

            'validate settings
            If Not _ValidSettings() Then
                'we read all of the values but do not 
                'have valid parameters and thus are unable 
                'to continue. Throw error to drop into exception catch
                '
                Throw New Exception("Invalid settings at start up. Unable to proceed.")
            End If

            'instantiate our common output queue object
            _QueueTxObject = New WA.DOL.VSD.WSPQueue.QueueObject(_QueueServer, _QueueTxName)


        Catch ex As Exception

            'LogEvent, Send E-mail, and quit
            Dim strMessage As String = "Service is unable to proceed. Shutting down. " & ex.Message
            'log the error
            _LogEvent("Service_OnStart", strMessage, WA.DOL.LogEvent.LogEvent.MessageType.Error, WA.DOL.LogEvent.LogEvent.LogType.Standard)

            OnStop()
            Exit Sub

        End Try

        'set our status to run mode
        _ServiceState = ServiceStates.Running

        'make note that we started
        _LogEvent("OnStart", "Service Started", WA.DOL.LogEvent.LogEvent.MessageType.Start, WA.DOL.LogEvent.LogEvent.LogType.Standard)

        'start an endless loop for processing the queue
        ThreadPool.QueueUserWorkItem(AddressOf _ProcessQueue)

    End Sub

    Protected Overrides Sub OnStop()
        ' Add code here to perform any tear-down necessary to stop your service.

        'warn threads we are shutting down
        _ServiceState = ServiceStates.Shutdown

        'give threads 20 seconds to wrap things up (this should be more than enough time)
        Dim EndWait As Date = Now.AddSeconds(20)
        While Now <= EndWait
            If _ThreadCount = 0 Then
                Exit While
            End If
        End While

        'log event that we are stopping
        _LogEvent("OnStop", "Service Stopped", WA.DOL.LogEvent.LogEvent.MessageType.Finish, WA.DOL.LogEvent.LogEvent.LogType.Standard)

    End Sub

    'Private Function _FixNLETs(ByVal Response As String) As String
    '    'emergency fix for WSP 4-29-2004 MTL
    '    'This should ONLY be called when the IsNLETs flag is True!

    '    Select Case Left(Response, 3)
    '        Case "BQ.", "YQ.", "AQ.", "BR.", "YH.", "AQ.", "BH.", "BD."
    '            'nothing preppended, return what was received
    '            Return Response
    '        Case Else
    '            'strip region field

    '            'L1WN    .BR.WABOAT000.AZNLETS22.BHN/WA23138B1963  NOT ON FILE     
    '            'T1RA  RR.BR.WA03100K8.WN6713K  NOT ON FILE  

    '            'find the first "." and remove everything to the left of it, including the period
    '            Dim Position As Integer = InStr(Response, ".")
    '            If Position > 1 Then
    '                Response = Right(Response, Len(Response) - Position)
    '            End If
    '            Return Response
    '    End Select

    'End Function

    Private Function _FixResponse(ByVal Response As String) As String
        'emergency fix for WSP 7-26-2004 MTL
        
        Select Case Left(Response, 3)
            Case "BQ.", "YQ.", "AQ.", "BR.", "YH.", "AQ.", "BH.", "BD."
                'nothing preppended, return what was received
                Return Response
            Case Else
                'strip region field

                'L1WN    .BR.WABOAT000.AZNLETS22.BHN/WA23138B1963  NOT ON FILE     
                'T1RA  RR.BR.WA03100K8.WN6713K  NOT ON FILE  

                'find the first "." and remove everything to the left of it, including the period
                Dim Position As Integer = InStr(Response, ".")
                If Position > 1 Then
                    Response = Right(Response, Len(Response) - Position)
                End If
                Return Response
        End Select

    End Function

    Private Sub _LogEvent(ByVal pstrSource As String, _
        ByVal pstrMessage As String, ByVal MessageType As WA.DOL.LogEvent.LogEvent.MessageType, _
        ByVal LogType As WA.DOL.LogEvent.LogEvent.LogType)
        'Purpose:   Write an event to the event log database of the specified type.
        'Input:     pstrSource = source procedure reporting the event.
        '           pstrMessage = event message.
        '           MessageType = LogEvent object indicator specifying whether the 
        '           message is error, informational, start, finish, or debug
        '           LogType = LogEvent object indicator
        'Returns:   None
        'Note:      When an LogType is error, an e-mail is automatically sent

        'log message
        _LogEventObject.LogEvent(Me.ServiceName, pstrSource, pstrMessage, MessageType, LogType)

        'if message type is an error, also log an email event
        If MessageType = WA.DOL.LogEvent.LogEvent.MessageType.Error Then
            _LogEventObject.LogEvent(Me.ServiceName, pstrSource, pstrMessage, MessageType, WA.DOL.LogEvent.LogEvent.LogType.Email)
        End If


    End Sub

    Private Sub _ProcessMessage(ByVal State As Object)
        'Purpose:   Worker thread process to run the Fujitsu program
        'Input:     State - new thread callback interface containing 
        '           a WSPMessage object.
        'Returns:   None.

        'Dim DEBUG_STRING As String = "1. Start Procedure: " & FormatDateTime(Now, DateFormat.LongTime) & "|"

        Const strUNAVAILABLE As String = "TEMPORARILY UNAVAILABLE "
        Dim WspRequest As New WA.DOL.VSD.WSPQueue.WSPMessage()
        Dim WspResponse As New WA.DOL.VSD.WSPQueue.WSPMessage()
        'get the object into a typed class
        WspRequest = CType(State, WA.DOL.VSD.WSPQueue.WSPMessage)
        WspRequest.Body = WspRequest.Body & " "

        If Left(WspRequest.Body, 2) = "BR" AndAlso InStr(WspRequest.Body, ".BHN/", CompareMethod.Text) > 1 Then
            'ACCESS switch is sending BHN/ NLETS codes with BR message keys - fix it
            WspRequest.Body = "BH" & Right(WspRequest.Body, Len(WspRequest.Body) - 2)
        End If

        'log the request
        If _DebugMode > 0 Then
            _LogEvent("_ProcessMessage", "Request: " & WspRequest.Body, WA.DOL.LogEvent.LogEvent.MessageType.Debug, WA.DOL.LogEvent.LogEvent.LogType.Standard)
        End If

        'pass message to Fujitsu EXE
        Dim strResponse As String = strUNAVAILABLE
        Dim MVBWSP As New dolservicedev1.DOLWebService()

        Try

            Dim Credentials As Net.NetworkCredential
            Dim Proxy As New System.Net.WebProxy()

            'set the credentials if present
            If _User <> "" AndAlso _Password <> "" AndAlso _Domain <> "" Then
                Credentials = New Net.NetworkCredential(_User, _Password, _Domain)
            ElseIf _UseSystemCredentials = True Then
                Credentials = System.Net.CredentialCache.DefaultCredentials()
            End If
            If Not Credentials Is Nothing Then
                MVBWSP.Credentials = Credentials
            End If

            'set the proxy if specified
            If _Proxy <> "" Then
                Proxy = New System.Net.WebProxy(_Proxy, True)
                If Not Credentials Is Nothing Then
                    Proxy.Credentials = Credentials
                End If
                MVBWSP.Proxy = Proxy
            End If

            'set PreAuthenticate
            MVBWSP.PreAuthenticate = _PreAuthenticate

            'part of emergency fix for WSP 4-29-2004 MTL
            Dim IsNLETs As Boolean = False

            'make the call
            Select Case Left(WspRequest.Body, 3)
                Case "BQ.", "YQ.", "AQ."
                    'NLETS
                    IsNLETs = True
                    strResponse = MVBWSP.CallMVBWSP(IIf(Left(WspRequest.Body, 5) <> "WN  .", "WN  .", "") & WspRequest.Body)
                Case Else
                    'DEBUG_STRING &= "2. Pre-WS Call: " & FormatDateTime(Now, DateFormat.LongTime) & "|"
                    strResponse = MVBWSP.CallMVBWSP(IIf(Left(WspRequest.Body, 5) <> "RARR.", "RARR.", "") & WspRequest.Body)
                    'DEBUG_STRING &= "3. Post-WS Call: " & FormatDateTime(Now, DateFormat.LongTime) & "|"
            End Select
            'expand LFs to CRLFs
            strResponse = Replace(Replace(strResponse, vbCr, ""), vbLf, vbCrLf)

            'trap SQL not available situation from Fujitsu
            If Trim(strResponse) = "" Then
                strResponse = strUNAVAILABLE
            End If

            'If IsNLETs = True Then
            '    'fix the NLETS response
            '    strResponse = _FixNLETs(strResponse)
            '    IsNLETs = False
            'End If
            'emergency fix for WSP 7-26-2004 MTL
            strResponse = _FixResponse(strResponse)



        Catch ex As Exception
            'if this fails, log event and send e-mail
            'no response message will be sent
            Dim strMessage As String = "Error from MVBWSP" & vbCrLf & "Request: " & IIf(Left(WspRequest.Body, 5) <> "RARR.", "RARR.", "") & WspRequest.Body & vbCrLf & vbCrLf & "Error: " & ex.Message
            'log the error
            _LogEvent("_ProcessMessage", strMessage, WA.DOL.LogEvent.LogEvent.MessageType.Error, WA.DOL.LogEvent.LogEvent.LogType.Standard)

            'handle null responses
            If Trim(strResponse) = "" Then
                strResponse = strUNAVAILABLE
            End If
        End Try

        'assign the response to the queue body
        WspResponse.Body = strResponse & " "

        WspResponse.MessageDate = Now 'set the current time
        WspResponse.Auxiliary = WspRequest.Auxiliary 'pass the Aux through
        WspResponse.Delimiter = WspRequest.Delimiter 'pass the delimiter through
        WspResponse.QueueName = _QueueTxName 'specify the output queue name (not required)
        WspResponse.OriginatingID = "" 'the vsMSSGatewayDB will fill this in
        WspResponse.Mnemonic = WspRequest.OriginatingID 'set the destination mnemonic to the originating ID

        'log the response
        If _DebugMode > 0 Then
            _LogEvent("_ProcessMessage", "Response: " & WspResponse.Body & vbCrLf & vbCrLf, WA.DOL.LogEvent.LogEvent.MessageType.Debug, WA.DOL.LogEvent.LogEvent.LogType.Standard)
        End If

        'lock the common send object and send the response
        SyncLock _QueueTxObject
            _QueueTxObject.SendMessage(WspResponse)
        End SyncLock

        'clean up
        WspRequest = Nothing
        WspResponse = Nothing

        'decrement the thread count
        Interlocked.Decrement(_ThreadCount)

        'DEBUG_STRING &= "4. End Procedure: " & FormatDateTime(Now, DateFormat.LongTime) & "|"
        '_LogEvent("ProcessMessage", "DEBUG: " & DEBUG_STRING, WA.DOL.LogEvent.LogEvent.MessageType.Debug, WA.DOL.LogEvent.LogEvent.LogType.Standard)
    End Sub

    Private Sub _ProcessQueue(ByVal State As Object)
        'Purpose: Main thread to monitor the queue for inquiries.

        'create a Rx object to read the queue
        Dim QueueRxObject As New WA.DOL.VSD.WSPQueue.QueueObject(_QueueServer, _QueueRxName)

        'loop here while service is running
        While _ServiceState = ServiceStates.Running

            Dim intAvailableThreads As Integer = 0
            Dim intIOThreads As Integer = 0

            'check resource availability
            ThreadPool.GetMaxThreads(intAvailableThreads, intIOThreads)

            Try
                '
                'if there is at least one message in the queue and we have resources, 
                'start reading the queue
                If QueueRxObject.CanRead = True AndAlso _ThreadCount < intAvailableThreads AndAlso _ThreadCount <= _MaxThreads Then
                    '
                    'read all of the messages in the queue or until we've been shutdown
                    Do While QueueRxObject.CanRead AndAlso _ServiceState = ServiceStates.Running

                        'make sure we still have resource availability
                        ThreadPool.GetMaxThreads(intAvailableThreads, intIOThreads)

                        If _ThreadCount >= intAvailableThreads Or _ThreadCount >= _MaxThreads Then
                            'bail out when no threads are available or max hit;
                            'this will ultimately drop us into Sleep mode
                            Exit Do
                        End If

                        'fetch the queue message
                        Dim QueueMessage As New WA.DOL.VSD.WSPQueue.WSPMessage()
                        QueueMessage = QueueRxObject.ReadMessage

                        'increment the thread count (each thread will decrement this when its done)
                        Interlocked.Increment(_ThreadCount)

                        'process each message on a separate thread
                        ThreadPool.QueueUserWorkItem(AddressOf _ProcessMessage, QueueMessage)
                    Loop
                Else
                    '
                    'queue is empty or currently at the max thread count, so take nap
                    Thread.Sleep(_QueueSleepWhenEmpty)
                End If
            Catch ex As Exception
                'log the response
                If _DebugMode > 0 Then
                    Dim strMessage As String = ex.Message & vbCrLf & vbCrLf & "Available Threads: " & CStr(intAvailableThreads) & vbCrLf & "Thread Count=" & CStr(_ThreadCount)
                    _LogEvent("_ProcessQueue", "Err: " & strMessage, WA.DOL.LogEvent.LogEvent.MessageType.Debug, WA.DOL.LogEvent.LogEvent.LogType.Standard)
                End If
                Thread.Sleep(_QueueSleepWhenEmpty * 10)
            End Try
        End While 'main loop

    End Sub

    Private Function _ReadAppSetting(ByVal pstrKey As String) As String
        'Purpose:   Retrieve parameter from app.config.
        'Input:     Key whose value is being sought.
        'Return:    String value of key.

        On Error Resume Next
        Dim AppSettingsReader As New System.Configuration.AppSettingsReader()
        Dim strReturnValue As String = ""
        pstrKey = Trim(pstrKey)
        If pstrKey <> "" Then
            'get the value
            strReturnValue = AppSettingsReader.GetValue(pstrKey, strReturnValue.GetType())
        End If
        AppSettingsReader = Nothing
        Return strReturnValue
    End Function

    Private Sub _ReadAppSettings()
        'Purpose:   Read the necessary application settings

        On Error Resume Next

        If _ReadAppSetting("QueueTx") <> "" Then
            _QueueTxName = _ReadAppSetting("QueueTx")
        End If

        If _ReadAppSetting("QueueRx") <> "" Then
            _QueueRxName = _ReadAppSetting("QueueRx")
        End If

        If _ReadAppSetting("QueueServer") <> "" Then
            _QueueServer = _ReadAppSetting("QueueServer")
        End If

        If IsNumeric(_ReadAppSetting("QueueSleepWhenEmpty")) AndAlso _
            CType(_ReadAppSetting("QueueSleepWhenEmpty"), Integer) > 0 Then
            _QueueSleepWhenEmpty = CType(_ReadAppSetting("QueueSleepWhenEmpty"), Integer)
        End If

        If IsNumeric(_ReadAppSetting("DebugMode")) AndAlso _
            CType(_ReadAppSetting("DebugMode"), Integer) > 0 AndAlso CType(_ReadAppSetting("DebugMode"), Integer) < 255 Then
            _DebugMode = CType(_ReadAppSetting("DebugMode"), Byte)
        End If

        If IsNumeric(_ReadAppSetting("MaxConcurrentThreads")) AndAlso _
            CType(_ReadAppSetting("MaxConcurrentThreads"), Integer) > 0 Then
            _MaxThreads = CType(_ReadAppSetting("MaxConcurrentThreads"), Integer)
        End If

        If _ReadAppSetting("Proxy") <> "" Then
            _Proxy = _ReadAppSetting("Proxy")
        End If

        If _ReadAppSetting("User") <> "" Then
            _User = _ReadAppSetting("User")
        End If

        If _ReadAppSetting("Password") <> "" Then
            _Password = _ReadAppSetting("Password")
        End If

        If _ReadAppSetting("Domain") <> "" Then
            _Domain = _ReadAppSetting("Domain")
        End If

        If LCase(_ReadAppSetting("UseSystemCredentials")) = "true" Then
            _UseSystemCredentials = True
        End If

        If LCase(_ReadAppSetting("PreAuthenticate")) = "true" Then
            _PreAuthenticate = True
        End If

    End Sub

    Private Sub _ReturnMessage(ByVal QueueMessage As WA.DOL.VSD.WSPQueue.WSPMessage)
        'Purpose:   used if we need to return a message to the queue
        Dim QueueRxObject As New WA.DOL.VSD.WSPQueue.QueueObject(_QueueServer, _QueueRxName)
        QueueRxObject.SendMessage(QueueMessage)
        QueueRxObject = Nothing
    End Sub

    Private Function _ValidSettings() As Boolean
        'Purpose:   Verify we have valid settings
        If _QueueTxName = "" OrElse _QueueRxName = "" OrElse _QueueServer = "" Then
            Return False
        End If
        Return True
    End Function

    Protected Overrides Sub OnShutdown()
        'calls the Windows service OnStop method

        OnStop()
    End Sub

End Class
