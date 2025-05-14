﻿using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Web;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using System.IO;

using System.Text.Json;
using System.Text.Json.Serialization;
using RulesEngineEditor.Shared;
using RulesEngineEditor.Models;
using System.Dynamic;
using RulesEngine.Models;
using RulesEngineEditor.Services;
using Omu.ValueInjecter;
using System.Net.Http.Json;
using System.Net.Http;
using System.Net.Http.Json;


namespace RulesEngineEditor.Pages
{
    public class WorkflowOption
    {
        public int Id { get; set; }
        public string WorkflowName { get; set; }
    }

    partial class RulesEngineEditorPage : ComponentBase
    {
        [Parameter]
        public string CurrentWorkflowName { get; set; } = "ALL";

        [Parameter]
        public RulesEngine.RulesEngine EditorRulesEngine { get { return _rulesEngine; } set { _rulesEngine = value; } }

        [Parameter]
        public Workflow[] Workflows { get; set; }

        private bool ShowWorkflows { get; set; } = true;
        public Dictionary<string, object> DownloadAttributes { get; set; }
        public Dictionary<string, object> DownloadInputAttributes { get; set; }

        JsonSerializerOptions jsonOptions;

        private RulesEngine.RulesEngine _rulesEngine = new RulesEngine.RulesEngine(new string[0], null);

        WorkflowData currentWorkflow = new WorkflowData();

        [Parameter]
        public EventCallback<Workflow[]> WorkflowsChanged { get; set; }

        [Parameter]
        public EventCallback<Workflow[]> WorkflowsSaved { get; set; }

        [Parameter]
        public List<WorkflowData> WorkflowDatas { get { return WorkflowService.Workflows; } set { if (WorkflowService.Workflows != value) { WorkflowService.Workflows = value; WorkflowUpdate(); } } }

        [Parameter]
        public EventCallback<List<WorkflowData>> WorkflowDatasChanged { get; set; }

        [Parameter]
        public EventCallback<RulesEngine.RulesEngine> OnRulesEngineInitialize { get; set; }

        private List<WorkflowOption> AvailableWorkflows = new();
        
        private string SelectedWorkflowName;


        bool WorkflowJSONHidden = false;

        string workflowJSONErrors;
        string _workflowJSON;
        string WorkflowsJSON { get { return _workflowJSON; } set { if (value != _workflowJSON) { _workflowJSON = value; WorkflowJSONChange(); } } }

        string inputJSONErrors;
        string _inputJSON;
        [Parameter]
        public string InputJSON { get { return _inputJSON; } set { _inputJSON = value; InputJSONUpdate(); RunRE(); } }

        [Parameter]
        public EventCallback<string> InputJSONChanged { get; set; }


        private List<MenuButton> menuButtons = new List<MenuButton> { new MenuButton("NewWorkflows"), new MenuButton("DownloadWorkflows"), new MenuButton("ImportWorkflows"), new MenuButton("AddWorkflow"), new MenuButton("SaveWorkflow", true), new MenuButton("NewInputs"), new MenuButton("DownloadInputs"), new MenuButton("ImportInputs"), new MenuButton("AddInput") };
        [Parameter]
        public List<MenuButton> MenuButtons { get { return menuButtons; } set { value.ForEach(v => menuButtons.Single(w => w.Name == v.Name).Enabled = v.Enabled); } }

        bool IsButtonEnabled(string name) { return MenuButtons.Single(w => w.Name == name).Enabled; }

        public bool sort_wf, sort_ip;

        protected override async Task OnInitializedAsync()
        {
            jsonOptions = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                IncludeFields = true,
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };

            WorkflowService.OnInputChange += InputUpdate;
            WorkflowService.OnWorkflowChange += WorkflowUpdate;
            await LoadWorkflow("DefaultWorkflow");
            await LoadWorkflowOptions();
            await base.OnInitializedAsync();
        }

        private async Task LoadWorkflowOptions()
        {
            try
            {
                AvailableWorkflows = await Http.GetFromJsonAsync<List<WorkflowOption>>("api/rules");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading workflows: " + ex.Message);
            }
        }

        protected override void OnParametersSet()
        {
            if (Workflows != default && string.IsNullOrEmpty(WorkflowsJSON))
            {
                var newJSON = JsonNormalizer.Normalize(
                    JsonSerializer.Serialize(Workflows, RulesEngineJsonSourceContext.Default.WorkflowArray)
                );

                if (newJSON != WorkflowsJSON)
                {
                    WorkflowService.Workflows = new List<WorkflowData>();
                    WorkflowsJSON = newJSON;
                    WorkflowJSONChange();
                    DownloadWorkflows();
                }
            }
        }

        private async Task LoadWorkflow(string workflowName = "DefaultWorkflow")
        {
            try
            {
                var result = await Http.GetFromJsonAsync<RulesWorkflow>($"/api/rules/{workflowName}");

                if (result != null && !string.IsNullOrWhiteSpace(result.RulesJson))
                {
                    WorkflowsJSON = JsonNormalizer.Normalize(result.RulesJson);
                    WorkflowJSONChange(); // existing method that deserializes and updates UI
                    await WorkflowsChanged.InvokeAsync(Workflows);
                    await WorkflowDatasChanged.InvokeAsync(WorkflowService.Workflows);
                }
                else
                {
                    Console.Error.WriteLine("Workflow not found or empty.");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading workflow: {ex.Message}");
            }
        }

        private async Task OnWorkflowSelected(ChangeEventArgs e)
        {
            SelectedWorkflowName = e.Value?.ToString();
            if (string.IsNullOrEmpty(SelectedWorkflowName))
                return;
            else{
                CurrentWorkflowName = SelectedWorkflowName;
                await LoadWorkflow(SelectedWorkflowName); // ✅ You must call this
            }

            try
            {
                var result = await Http.GetFromJsonAsync<RulesWorkflow>($"api/rules/{SelectedWorkflowName}");
                WorkflowsJSON = result?.RulesJson ?? "";
                WorkflowJSONChange();
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error loading selected workflow: " + ex.Message);
            }
        }


        public void Dispose()
        {
            WorkflowService.OnInputChange -= InputUpdate;
            WorkflowService.OnWorkflowChange -= WorkflowUpdate;
        }

        private void DeleteWorkflow(WorkflowData workflow)
        {
            WorkflowService.Workflows.Remove(workflow);
            WorkflowService.WorkflowUpdate();
        }
        void DeleteInput(InputRuleParameter input)
        {
            WorkflowService.Inputs.Remove(input);
            WorkflowService.WorkflowUpdate();
        }
        void UpdateInputDelete(InputRuleParameter input)
        {
            WorkflowService.Inputs.Remove(input);
        }
        public void NewWorkflows()
        {
            WorkflowService.Workflows = new List<WorkflowData>();
            WorkflowService.RuleParameters = new RuleParameter[0];
            WorkflowService.Inputs = new List<InputRuleParameter>();
            WorkflowsJSON = "";
            InputJSON = "";
            WorkflowsChanged.InvokeAsync(Workflows);
            WorkflowDatasChanged.InvokeAsync(WorkflowService.Workflows);
            AddWorkflow();
            StateHasChanged();
        }

        public void NewGlobalParam(WorkflowData wf)
        {
            if (wf.GlobalParams == null)
            {
                wf.GlobalParams = new List<ScopedParamData>();
            }
            wf.GlobalParams.Insert(0, new ScopedParamData());
        }

        private void AddWorkflow()
        {
            WorkflowData workflow = new WorkflowData();
            workflow.GlobalParams = new List<ScopedParamData>();
            workflow.Rules = new List<RuleData>();
            workflow.Seq = -1;
            WorkflowService.Workflows.Insert(0, workflow);
            StateHasChanged();
        }

        private void SaveWorkflow_Old()
        {
            WorkflowUpdate();
            WorkflowsSaved.InvokeAsync(Workflows);
        }

        private async void SaveWorkflow()
        {
            WorkflowUpdate();

            var workflows = JsonSerializer.Deserialize<Workflow[]>(WorkflowsJSON, RulesEngineJsonSourceContext.Default.WorkflowArray);

            var payload = new
            {
                WorkflowName = workflows[0].WorkflowName,
                RulesJson = WorkflowsJSON
            };

            try
            {
                Console.WriteLine("------------------------------------------------");
                Console.WriteLine(JsonSerializer.Serialize(payload));
                Console.WriteLine("------------------------------------------------");

                var response = await Http.PostAsJsonAsync("http://localhost:5000/api/rules", payload);
                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("Saved to DB successfully.");
                }
                else
                {
                    Console.Error.WriteLine("Failed to save to DB: " + response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Error saving to DB: " + ex.Message);
            }

            await WorkflowsSaved.InvokeAsync(workflows);
        }



        private void NewInputs()
        {
            WorkflowService.Inputs = new List<InputRuleParameter>();
            StateHasChanged();
        }

        private void AddInput()
        {
            InputRuleParameter input = new InputRuleParameter();
            input.InputRuleName = $"Input{WorkflowService.Inputs.Count + 1}";
            InputParameter parameter = new InputParameter();
            parameter.Name = "param1";
            input.Parameters.Add(parameter);
            WorkflowService.Inputs.Insert(0, input);
            StateHasChanged();
        }

        private void WorkflowUpdate()
        {
            DownloadWorkflows();
            UpdateInputs();
            DownloadInputs();
            RunRE();
            WorkflowDatasChanged.InvokeAsync(WorkflowService.Workflows);
            StateHasChanged();
        }

        private void InputUpdate()
        {
            UpdateInputs();
            DownloadInputs();
            RunRE();
            InputJSONChanged.InvokeAsync(InputJSON);
            StateHasChanged();
        }

        private void UpdateInputs()
        {
            var serializationOptions = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };

            inputJSONErrors = "";
            List<InputRuleParameterDictionary> newInputs = new List<InputRuleParameterDictionary>();
            WorkflowService.Inputs.ForEach(i =>
            {
                InputRuleParameterDictionary newInput = new InputRuleParameterDictionary();
                newInput.InputRuleName = i.InputRuleName;
                newInput.Parameters = new Dictionary<string, object>();
                foreach (var p in i.Parameters)
                {
                    try
                    {
                        newInput.Parameters.Add(p.Name, JsonSerializer.Deserialize<dynamic>(p.Value, new JsonSerializerOptions
                        {
                            Converters = { new DynamicJsonConverter() }
                        }));
                    }
                    catch (Exception ex)
                    {
                        inputJSONErrors += ex.Message + " ";
                    }
                }
                newInputs.Add(newInput);
            });

            if (inputJSONErrors == "")
            {
                InputJSON = JsonNormalizer.Normalize(JsonSerializer.Serialize(newInputs, RulesEngineJsonSourceContext.Default.Options));
            }
        }

        private void RunRE()
        {
            workflowJSONErrors = "";
            try
            {
                //TODO Reverted to Newtonsoft - roll forward to System.Text.Json when it's fully supported (Github Pages PWA fails without Newtonsoft)
                //var Workflows = Newtonsoft.Json.JsonConvert.DeserializeObject<Workflow[]>(WorkflowJSON);
                var Workflows = JsonSerializer.Deserialize(
                    WorkflowsJSON,
                    RulesEngineJsonSourceContext.Default.WorkflowArray
                );
                if (WorkflowService.RuleParameters.Length == 0) return;

                _rulesEngine.ClearWorkflows();
                _rulesEngine.AddOrUpdateWorkflow(Workflows);

                List<RuleResultTree> resultList = new List<RuleResultTree>();
                WorkflowService.Workflows.ForEach(async workflow =>
                {
                    try
                    {
                        resultList = await _rulesEngine.ExecuteAllRulesAsync(workflow.WorkflowName, WorkflowService.RuleParameters);
                    }
                    catch (Exception ex)
                    {
                        workflowJSONErrors += ex.Message + " ";
                        return;
                    }

                    for (int i = 0; i < resultList.Count; i++)
                    {
                        var rule = workflow.Rules.FirstOrDefault(r => r.RuleName == resultList[i].Rule.RuleName);
                        rule.IsSuccess = resultList[i].IsSuccess;
                        if (!(bool)rule.IsSuccess)
                        {
                            rule.ExceptionMessage = resultList[i].ExceptionMessage;
                        }
                        else
                        {
                            rule.ExceptionMessage = "Rule was successful.";
                        }
                    }
                });
            }
            catch (Exception ex)
            {
                workflowJSONErrors += ex.Message + " ";
            }
            StateHasChanged();
        }

        private async void WorkflowDragEnd(WorkflowData wf)
        {
            WorkflowService.Sort(WorkflowService.Workflows);
            await WorkflowsChanged.InvokeAsync(Workflows);
            await WorkflowDatasChanged.InvokeAsync(WorkflowService.Workflows);
        }
        private async void ImportWorkflows(InputFileChangeEventArgs files)
        {
            var selectedFile = files.File;
            StreamReader sr = new StreamReader(selectedFile.OpenReadStream());
            WorkflowsJSON = JsonNormalizer.Normalize(await sr.ReadToEndAsync());

            WorkflowJSONChange();
            await WorkflowsChanged.InvokeAsync(Workflows);
            await WorkflowDatasChanged.InvokeAsync(WorkflowService.Workflows);
            StateHasChanged();
        }

        private void WorkflowJSONChange()
        {
            workflowJSONErrors = "";
            try
            {
                var workflows = JsonSerializer.Deserialize(
                    WorkflowsJSON,
                    RulesEngineJsonSourceContext.Default.ListWorkflowData
                );

                if (!WorkflowService.Workflows.Any())
                {
                    WorkflowService.Workflows = workflows;
                }
                else
                {
                    Mapper.Map<WorkflowData>(workflows, WorkflowService.Workflows);
                }

                RunRE();
            }
            catch (Exception ex)
            {
                workflowJSONErrors = ex.Message;
            }
        }

        private void DownloadWorkflows()
        {
            workflowJSONErrors = "";
            var jsonString = JsonSerializer.Serialize(
                WorkflowService.Workflows,
                RulesEngineJsonSourceContext.Default.ListWorkflowData
            );
            if (jsonString == "[]")
            {
                return;
            }
            WorkflowsJSON = JsonNormalizer.Normalize(jsonString);

            try
            {
                //ensure no serialzable errors in JSON before enabling download
                var re = new RulesEngine.RulesEngine(
                    JsonSerializer.Deserialize(WorkflowsJSON, RulesEngineJsonSourceContext.Default.WorkflowArray)
                );

                DownloadAttributes = new Dictionary<string, object>();
                DownloadAttributes.Add("href", "data:text/plain;charset=utf-8," + WorkflowsJSON);
                DownloadAttributes.Add("download", "RulesEngine.json");
            }
            catch (Exception ex)
            {
                workflowJSONErrors = ex.Message;
            }
        }
        private async void ImportInputs(InputFileChangeEventArgs files)
        {
            var selectedFile = files.File;
            StreamReader sr = new StreamReader(selectedFile.OpenReadStream());
            InputJSON = JsonNormalizer.Normalize(await sr.ReadToEndAsync());
            InputJSONUpdate();
            ShowWorkflows = true;
            WorkflowService.WorkflowUpdate();
        }

        [Obsolete("InputRule is deprecated. Use InputRuleName instead.")]
        private void InputJSONUpdate()
        {
            inputJSONErrors = "";
            try
            {
                var inputs = JsonSerializer.Deserialize<dynamic>(InputJSON);

                WorkflowService.Inputs = new List<InputRuleParameter>();

                List<RuleParameter> ruleParameters = new List<RuleParameter>();
                foreach (var x in inputs.EnumerateArray())
                {
                    string key = "";
                    dynamic value = null;

                    try
                    {
                        JsonElement i = JsonSerializer.Deserialize<dynamic>(x.ToString(), jsonOptions);

                        key = i.GetProperty("InputRuleName").GetString();
                        value = i.GetProperty("Parameters");
                    }
                    catch (Exception ex)
                    {
                        inputJSONErrors += " " + ex.Message;
                    }

                    InputRuleParameter input = new InputRuleParameter();
                    input.InputRuleName = key;
                    input.Parameters = new List<InputParameter>();

                    var values = JsonSerializer.Deserialize<ExpandoObject>(
                        JsonSerializer.Serialize(value), new JsonSerializerOptions
                        {
                            Converters = { new DynamicJsonConverter() }
                        });

                    foreach (KeyValuePair<string, object> v in values)
                    {
                        InputParameter param = new InputParameter();
                        param.Name = v.Key;
                        param.Value = JsonSerializer.Serialize(v.Value);

                        input.Parameters.Add(param);
                    }
                    WorkflowService.Inputs.Add(input);
                    ruleParameters.Add(new RuleParameter(key, values));
                }
                WorkflowService.RuleParameters = ruleParameters.ToArray();
            }
            catch (Exception ex)
            {
                inputJSONErrors = ex.Message;
            }
        }

        private void DownloadInputs()
        {
            DownloadInputAttributes = new Dictionary<string, object>();
            DownloadInputAttributes.Add("href", "data:text/plain;charset=utf-8," + JsonNormalizer.Normalize(InputJSON));
            DownloadInputAttributes.Add("download", "RulesEngineInputs.json");
        }
    }
}
