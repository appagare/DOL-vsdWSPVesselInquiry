﻿'------------------------------------------------------------------------------
' <autogenerated>
'     This code was generated by a tool.
'     Runtime Version: 1.0.3705.288
'
'     Changes to this file may cause incorrect behavior and will be lost if 
'     the code is regenerated.
' </autogenerated>
'------------------------------------------------------------------------------

Option Strict Off
Option Explicit On

Imports System
Imports System.ComponentModel
Imports System.Diagnostics
Imports System.Web.Services
Imports System.Web.Services.Protocols
Imports System.Xml.Serialization

'
'This source code was auto-generated by Microsoft.VSDesigner, Version 1.0.3705.288.
'
Namespace dolwkd092731
    
    '<remarks/>
    <System.Diagnostics.DebuggerStepThroughAttribute(),  _
     System.ComponentModel.DesignerCategoryAttribute("code"),  _
     System.Web.Services.WebServiceBindingAttribute(Name:="WSMVBWSPSoap", [Namespace]:="http://tempuri.org/")>  _
    Public Class WSMVBWSP
        Inherits System.Web.Services.Protocols.SoapHttpClientProtocol
        
        '<remarks/>
        Public Sub New()
            MyBase.New
            Dim urlSetting As String = System.Configuration.ConfigurationSettings.AppSettings("vsdWSPVesselInquiry.dolwkd092731.WSMVBWSP")
            If (Not (urlSetting) Is Nothing) Then
                Me.Url = String.Concat(urlSetting, "")
            Else
                Me.Url = "http://dolwkd092731/UARMVBWSP/WSMVBWSP.asmx"
            End If
        End Sub
        
        '<remarks/>
        <System.Web.Services.Protocols.SoapDocumentMethodAttribute("http://tempuri.org/Call-MVBWSP", RequestElementName:="Call-MVBWSP", RequestNamespace:="http://tempuri.org/", ResponseElementName:="Call-MVBWSPResponse", ResponseNamespace:="http://tempuri.org/", Use:=System.Web.Services.Description.SoapBindingUse.Literal, ParameterStyle:=System.Web.Services.Protocols.SoapParameterStyle.Wrapped)>  _
        Public Function CallMVBWSP(<System.Xml.Serialization.XmlElementAttribute("INPUT-VAL")> ByVal INPUTVAL As String) As <System.Xml.Serialization.XmlElementAttribute("Call-MVBWSPResult")> String
            Dim results() As Object = Me.Invoke("CallMVBWSP", New Object() {INPUTVAL})
            Return CType(results(0),String)
        End Function
        
        '<remarks/>
        Public Function BeginCallMVBWSP(ByVal INPUTVAL As String, ByVal callback As System.AsyncCallback, ByVal asyncState As Object) As System.IAsyncResult
            Return Me.BeginInvoke("CallMVBWSP", New Object() {INPUTVAL}, callback, asyncState)
        End Function
        
        '<remarks/>
        Public Function EndCallMVBWSP(ByVal asyncResult As System.IAsyncResult) As String
            Dim results() As Object = Me.EndInvoke(asyncResult)
            Return CType(results(0),String)
        End Function
    End Class
End Namespace
