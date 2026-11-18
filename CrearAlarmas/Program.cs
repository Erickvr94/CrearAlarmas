//ruta @"C:\Users\Siemens\Documents\Automation\PruebaCreateLoggingTags\Taura4\Taura4.ap19"

using Newtonsoft.Json;
using Siemens.Engineering;
using Siemens.Engineering.Hmi.Cycle;
using Siemens.Engineering.Hmi.Tag;
using Siemens.Engineering.Hmi.TextGraphicList;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.HmiUnified.HmiAlarm;
using Siemens.Engineering.HmiUnified.HmiAlarm.HmiAlarmCommon;
using Siemens.Engineering.HmiUnified.HmiLogging;
using Siemens.Engineering.HmiUnified.HmiLogging.HmiLoggingCommon;
using Siemens.Engineering.HmiUnified.HmiOpcUaAlarm;
using Siemens.Engineering.HmiUnified.HmiTags;
using Siemens.Engineering.HmiUnified.LoggingTags;
using Siemens.Engineering.HW;
using Siemens.Engineering.HW.Features;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Claims;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;


namespace UserProgram
{
    internal class MyProgram
    {
        public static void Main(string[] args)
        {
            string projectPath = @"C:\Users\Automatismo\Documents\Automation\SCADAIPSP_NOMBRES\SCADA_TAGS_MODIFICADOS\Scada_IPSP\Scada_IPSP.ap18";//Ruta del proyecto TIA Portal
            var tiaPortal = new TiaPortal(TiaPortalMode.WithUserInterface);//Iniciar TIA Portal con interfaz de usuario
            var datos = CargarDatos();//Cargar los datos de las piscinas y tableros desde el JSON
            Project project = tiaPortal.Projects.Open(new FileInfo(projectPath), MyUmacDelegate);//Abrir el proyecto TIA Portal con las credenciales UMAC
            if (project != null)
            {
                Console.WriteLine("Proyecto abierto!");
                try
                {
                    HmiSoftware hmiSoftware = GetHmiSoftware(project);//Obtener el software HMI del proyecto
                    if (hmiSoftware != null)
                    {
                        // Puedes mantener un identificador de sector para los nombres de los Logs o Alarm Classes si lo deseas
                        string sector = "TAURAII";
                        Console.WriteLine("HMI encontrado: " + hmiSoftware.Name);

                        //DataLogs
                        DatalogCreate(project, $"HorasTrabajadasAireador{sector}");
                        DatalogCreate(project, $"ParametrosElectricos{sector}");
                        DatalogCreate(project, $"StatusTablero{sector}");
                        DatalogCreate(project, $"EnergiaConsumida{sector}");
                        DatalogCreate(project, $"EnergiaConsumidaPiscina{sector}");

                        //Editar propiedades DataLogs
                        DatalogProperties(project, $"HorasTrabajadasAireador{sector}", sector);
                        DatalogProperties(project, $"ParametrosElectricos{sector}", sector);
                        DatalogProperties(project, $"StatusTablero{sector}", sector);
                        DatalogProperties(project, $"EnergiaConsumida{sector}", sector);
                        DatalogProperties(project, $"EnergiaConsumidaPiscina{sector}", sector);

                        //AlarmLogs y propiedades
                        AlarmLogCreate(project, $"Alarmas{sector}");
                        AlarmLogCreate(project, $"Operacion{sector}");
                        AlarmLogCreate(project, $"Eventos{sector}");

                        foreach (var piscina in datos)
                        {
                            string piscinaName = piscina.Key;
                            Console.WriteLine("Procesando Piscina: " + piscinaName);

                            // Se eliminó el prefijo {sector}_ del tag principal
                            LoggingTagCreate(project, $"EnergiaConsumidaPiscina{sector}", $"EnergiaDia_" + piscinaName, sector);
                            DiscreteAlarmCreatePis(project, $"{sector}", piscinaName);

                            foreach (var tablero in piscina.Value)
                            {
                                string tableroName = tablero.Key;
                                Console.WriteLine("Procesando Tablero: " + tableroName);
                                DiscreteAlarmCreateTab(project, $"{sector}", tableroName);
                                AnalogAlarmCreate(project, $"{sector}", tableroName);

                                //Estados de los tableros
                                LoggingTagCreateTablero(project, $"StatusTablero{sector}", tableroName + ".Num_Air_On", sector);

                                if (tablero.Value.NameTCP != "")
                                {
                                    DiscreteAlarmCreateTabTCP(project, $"{sector}", tableroName);

                                    //Energia Consumida TCP
                                    LoggingTagCreateTablero(project, $"EnergiaConsumida{sector}", tableroName + ".Ener_Consu", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".Corriente_L1", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".Corriente_L2", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".Corriente_L3", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".Factor_Pot", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".Pot_Trif", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".THDI_tot", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".THDV_tot", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".Volt_L1L2", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".Volt_L2L3", sector);
                                    LoggingTagCreateTablero(project, $"ParametrosElectricos{sector}", tableroName + ".Volt_L3L1", sector);
                                }
                                int CantCirc = Int32.Parse(tablero.Value.CantCirc);
                                for (int i = 0; i < CantCirc; i++)
                                {
                                    //Horas trabajadas aireadores
                                    LoggingTagCreateTableroAir(project, $"HorasTrabajadasAireador{sector}", tableroName + "_Horas_trabajo", i, sector);
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine("No se encontró el proyecto");
                    }
                }
                finally
                {
                    //project.Save();
                    //project.Close();
                }
            }
        }

        private static void MyUmacDelegate(UmacCredentials umacCredentials)
        {
            SecureString password = new SecureString();
            foreach (char c in "Melacorp?01")
            {
                password.AppendChar(c);
            }
            password.MakeReadOnly();
            umacCredentials.Type = UmacUserType.Project;
            umacCredentials.Name = "Santos";
            umacCredentials.SetPassword(password);
        }

        private static HmiSoftware GetHmiSoftware(Project project)
        {
            foreach (Device device in project.Devices)
            {
                foreach (DeviceItem deviceItem in device.DeviceItems)
                {
                    SoftwareContainer container = deviceItem.GetService<SoftwareContainer>();
                    if (container?.Software is HmiSoftware hmi)
                    {
                        return hmi;
                    }
                }
            }
            return null;
        }

        private static void DatalogCreate(Project project, String dataLogName)
        {
            Console.WriteLine("Creando DataLog: " + dataLogName);
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiDataLogComposition dataLogs = hmiSoftware.DataLogs;
            HmiDataLog dataLog = dataLogs.Create(dataLogName);
        }

        private static void DatalogProperties(Project project, String dataLogName, String sector)
        {
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiDataLogComposition dataLogs = hmiSoftware.DataLogs;
            HmiDataLog dataLog = dataLogs.Find(dataLogName);
            string name = dataLog.Name;

            DeviceNode dv = DeviceNode.Local;
            dataLog.Settings.StorageDevice = dv;
            dataLog.Settings.StorageFolder = name;

            if (name.Equals($"ParametrosElectricos{sector}"))
            {
                dataLog.Segment.SegmentMaxSize = 36;
                dataLog.Settings.LogMaxSize = 288;
            }
            else
            {
                dataLog.Segment.SegmentMaxSize = 12;
                dataLog.Settings.LogMaxSize = 96;
            }

            LogDuration duration = dataLog.Settings.LogTimePeriod;
            duration.Days = 120;
            SegmentDuration segmentDuration = dataLog.Segment.SegmentTimePeriod;
            segmentDuration.Days = 15;
            dataLog.Segment.SegmentStartTime = DateTime.Now;
        }

        private static void LoggingTagProperties(Project project, String dataLogName, HmiLoggingTag loggingTag, String sector)
        {
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiDataLogComposition dataLogs = hmiSoftware.DataLogs;
            HmiDataLog dataLog = dataLogs.Find(dataLogName);
            string name = dataLog.Name;

            if (name.Equals($"ParametrosElectricos{sector}"))
            {
                loggingTag.SmoothingMode = HmiSmoothingMode.NoSmoothing;
                loggingTag.LimitScope = HmiLimitScope.NoLimitsUsed;
                loggingTag.AggregationMode = HmiAggregationMode.NoAggregation;
                loggingTag.LoggingMode = HmiLoggingMode.Cyclic;
                loggingTag.Cycle = "T15m";
            }
            else
            {
                loggingTag.SmoothingMode = HmiSmoothingMode.NoSmoothing;
                loggingTag.LimitScope = HmiLimitScope.NoLimitsUsed;
                loggingTag.AggregationMode = HmiAggregationMode.NoAggregation;
                loggingTag.LoggingMode = HmiLoggingMode.Cyclic;
                loggingTag.Cycle = "T1h";
            }
        }

        private static void LoggingTagCreateTablero(Project project, String dataLog, String tag, String sector)
        {
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiTagComposition hmiTags = hmiSoftware.Tags;
            String[] pis = tag.Split('.');
            var tagP = hmiSoftware.Tags.Find(pis[0]);
            var sub = tagP.Members.Find(pis[1]);
            HmiLoggingTagComposition loggingTags = sub.LoggingTags;
            HmiLoggingTag loggingTag = loggingTags.Create(pis[0] + "_" + pis[1]);
            loggingTag.DataLog = dataLog;
            LoggingTagProperties(project, dataLog, loggingTag, sector);
        }

        private static void LoggingTagCreate(Project project, String dataLog, String tag, String sector)
        {
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiTagComposition hmiTags = hmiSoftware.Tags;
            HmiTag hmiTag = hmiTags.Find(tag);
            HmiLoggingTagComposition loggingTags = hmiTag.LoggingTags;
            HmiLoggingTag loggingTag = loggingTags.Create(tag);
            loggingTag.DataLog = dataLog;
            LoggingTagProperties(project, dataLog, loggingTag, sector);
        }

        private static void LoggingTagCreateTableroAir(Project project, String dataLog, String tag, int i, String sector)
        {
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiTagComposition hmiTags = hmiSoftware.Tags;
            var tagP = hmiSoftware.Tags.Find(tag);
            var sub = tagP.Members.Find("[" + i + "]");
            HmiLoggingTagComposition loggingTags = sub.LoggingTags;
            HmiLoggingTag loggingTag = loggingTags.Create(tag + i);
            loggingTag.DataLog = dataLog;
            LoggingTagProperties(project, dataLog, loggingTag, sector);
        }

        private static void AlarmLogCreate(Project project, String alarmName)
        {
            Console.WriteLine("Creando AlarmLog: " + alarmName);
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiAlarmLogComposition alarmLogs = hmiSoftware.AlarmLogs;
            HmiAlarmLog alarmLog = alarmLogs.Create(alarmName);

            DeviceNode dv = DeviceNode.Local;
            alarmLog.Settings.StorageDevice = dv;
            alarmLog.Segment.SegmentMaxSize = 20;
            alarmLog.Settings.LogMaxSize = 300;

            LogDuration duration = alarmLog.Settings.LogTimePeriod;
            duration.Days = 30;
            SegmentDuration segmentDuration = alarmLog.Segment.SegmentTimePeriod;
            segmentDuration.Days = 2;
            alarmLog.Segment.SegmentStartTime = DateTime.Now;
        }

        private static void DiscreteAlarmCreatePis(Project project, String area, String pis)
        {
            String[] alarmPis = new string[]
            {
                "Modo_Prioridad","Status_Habilitada","Status_Pescada","Status_Transferida","Status_Raleo"
            };

            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiDiscreteAlarmComposition discreteAlarms = hmiSoftware.DiscreteAlarms;

            foreach (string alarmType in alarmPis)
            {
                HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + pis);
                discreteAlarm.Area = "HMI_RT_1::Alarming";
                discreteAlarm.AlarmClass = area + "_Operacion";

                if (alarmType == "Modo_Prioridad")
                {
                    discreteAlarm.EventText.Items[0].Text = "<body><p>Activado/Desactivado Prioridad Piscina " + pis + "</p></body>";
                    // Removido "area + "_"
                    discreteAlarm.RaisedStateTag = pis + ".Prioridad";
                    discreteAlarm.RaisedStateTagBitNumber = 0;
                }
                else
                {
                    // Removido "area + "_"
                    discreteAlarm.RaisedStateTag = "Status_" + pis;

                    if (alarmType == "Status_Habilitada")
                    {
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Status de Piscina " + pis + ": Habilitada</p></body>";
                        discreteAlarm.RaisedStateTagBitNumber = 1;
                    }
                    else if (alarmType == "Status_Pescada")
                    {
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Status de Piscina " + pis + ": Pescada</p></body>";
                        discreteAlarm.RaisedStateTagBitNumber = 2;
                    }
                    else if (alarmType == "Status_Transferida")
                    {
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Status de Piscina " + pis + ": Transferida</p></body>";
                        discreteAlarm.RaisedStateTagBitNumber = 3;
                    }
                    else
                    {
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Status de Piscina " + pis + ": Raleo</p></body>";
                        discreteAlarm.RaisedStateTagBitNumber = 4;
                    }
                }

                discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
            }
        }

        private static void DiscreteAlarmCreateTab(Project project, String area, String tab)
        {
            uint[] idxCircuitoOnOff = new uint[]
            {
                29, 30, 31, 16, 17, 18, 19, 20, 21, 8, 9, 10
            };

            String[] alarmTypes = new string[]
            {
                "Circui_On_Off","Desactiva_Circuito","Falla_GM_Individual","Falla_Guardamotor",
                "Supervisor_Voltaje","Supervisor_Trasien","Paro_Emergencia","Comunic_PLC","Comunic_ANT",
                "Manu_Auto_Remot_Piscina","On_Off_Remot_Piscina","Pis_On_Off","Modo_Oxigeno_Horario_On_Off"
            };

            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiDiscreteAlarmComposition discreteAlarms = hmiSoftware.DiscreteAlarms;

            foreach (string alarmType in alarmTypes)
            {
                if (alarmType == "Circui_On_Off")
                {
                    for (uint i = 1; i <= 12; i++)
                    {
                        HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab + "_" + i);
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Circuito " + tab + "_" + i + "</p></body>";
                        discreteAlarm.Area = "HMI_RT_1::Alarming";
                        discreteAlarm.AlarmClass = area + "_Circ_On_Off";
                        discreteAlarm.RaisedStateTag = tab + ".Inputs";
                        discreteAlarm.RaisedStateTagBitNumber = idxCircuitoOnOff[i - 1];
                        discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                    }
                }
                else if (alarmType == "Desactiva_Circuito")
                {
                    for (uint i = 1; i <= 12; i++)
                    {
                        HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab + "_" + i);
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Desactiva Circuito " + tab + "_" + i + "</p></body>";
                        discreteAlarm.Area = "HMI_RT_1::Alarming";
                        discreteAlarm.AlarmClass = area + "_Operacion";
                        discreteAlarm.RaisedStateTag = tab + ".Desactiva_GM";
                        discreteAlarm.RaisedStateTagBitNumber = i - 1;
                        discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                    }
                }
                else if (alarmType == "Falla_GM_Individual")
                {
                    for (uint i = 1; i <= 12; i++)
                    {
                        HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab + "_" + i);
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Alarma Falla Guardamotor Circuito " + tab + "_" + i + "</p></body>";
                        discreteAlarm.Area = "HMI_RT_1::Alarming";
                        discreteAlarm.AlarmClass = area + "_Alarm_Guarda";
                        discreteAlarm.RaisedStateTag = tab + ".Falla_GM_Individual";
                        discreteAlarm.RaisedStateTagBitNumber = i - 1;
                        discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                    }
                }
                else if (alarmType == "Falla_Guardamotor")
                {
                    HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab);
                    discreteAlarm.EventText.Items[0].Text = "<body><p>Alarma Falla Guardamotor " + tab + "</p></body>";
                    discreteAlarm.Area = "HMI_RT_1::Alarming";
                    discreteAlarm.AlarmClass = area + "_Alarm_Guarda";
                    discreteAlarm.RaisedStateTag = tab + ".Inputs";
                    discreteAlarm.RaisedStateTagBitNumber = 28;
                    discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                }
                else if (alarmType == "Supervisor_Voltaje")
                {
                    HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab);
                    discreteAlarm.EventText.Items[0].Text = "<body><p>Alarma Supervisor Voltaje " + tab + "</p></body>";
                    discreteAlarm.Area = "HMI_RT_1::Alarming";
                    discreteAlarm.AlarmClass = area + "_Alarm_Sup_Volt";
                    discreteAlarm.RaisedStateTag = tab + ".Inputs";
                    discreteAlarm.RaisedStateTagBitNumber = 27;
                    discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                }
                else if (alarmType == "Supervisor_Trasien")
                {
                    HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab);
                    discreteAlarm.EventText.Items[0].Text = "<body><p>Alarma Supervisor Trasientes " + tab + "</p></body>";
                    discreteAlarm.Area = "HMI_RT_1::Alarming";
                    discreteAlarm.AlarmClass = area + "_Alarm_Sup_Tras";
                    discreteAlarm.RaisedStateTag = tab + ".Inputs";
                    discreteAlarm.RaisedStateTagBitNumber = 26;
                    discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                }
                else if (alarmType == "Paro_Emergencia")
                {
                    HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab);
                    discreteAlarm.EventText.Items[0].Text = "<body><p>Alarma Paro Emergencia " + tab + "</p></body>";
                    discreteAlarm.Area = "HMI_RT_1::Alarming";
                    discreteAlarm.AlarmClass = area + "_Stop_Parada";
                    discreteAlarm.RaisedStateTag = tab + ".Inputs";
                    discreteAlarm.RaisedStateTagBitNumber = 11;
                    discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                }
                else if (alarmType == "Comunic_PLC")
                {
                    HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab);
                    discreteAlarm.EventText.Items[0].Text = "<body><p>Alarma Perdida Comun PLC " + tab + "</p></body>";
                    discreteAlarm.Area = "HMI_RT_1::Alarming";
                    discreteAlarm.AlarmClass = area + "_Comunicaciones";
                    discreteAlarm.RaisedStateTag = tab + ".Stat_Comm";
                    discreteAlarm.RaisedStateTagBitNumber = 0;
                    discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                }
                else if (alarmType == "Comunic_ANT")
                {
                    HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab);
                    discreteAlarm.EventText.Items[0].Text = "<body><p>Alarma Perdida Comun ANT " + tab + "</p></body>";
                    discreteAlarm.Area = "HMI_RT_1::Alarming";
                    discreteAlarm.AlarmClass = area + "_Comunicaciones";

                    // Modificación: El tab (ej: 1B1_TA01) solo tiene 2 partes ahora, no 3
                    string[] res = tab.Split('_');
                    discreteAlarm.RaisedStateTag = "Ant_Stat_"+ res[0] +"_" + res[1];
                    discreteAlarm.RaisedStateTagBitNumber = 0;
                    discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                }
                else
                {
                    HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmType + "_" + tab);
                    if (alarmType == "Manu_Auto_Remot_Piscina")
                    {
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Operación Auto-Manual " + tab + "</p></body>";
                        discreteAlarm.RaisedStateTag = tab + ".Bloq_Auto_Hora";
                    }
                    else if (alarmType == "On_Off_Remot_Piscina")
                    {
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Remoto ON/OFF " + tab + "</p></body>";
                        discreteAlarm.RaisedStateTag = tab + ".Run_Rem";
                    }
                    else if (alarmType == "Pis_On_Off")
                    {
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Piscina Encendida " + tab + "</p></body>";
                        discreteAlarm.RaisedStateTag = tab + ".Algun_Air_On";
                    }
                    else if (alarmType == "Modo_Oxigeno_Horario_On_Off")
                    {
                        discreteAlarm.EventText.Items[0].Text = "<body><p>Oxigenación por horarios Activado " + tab + "</p></body>";
                        discreteAlarm.RaisedStateTag = tab + ".Modo_Oxigeno_Horario";
                    }
                    discreteAlarm.AlarmClass = area + "_Operacion";
                    discreteAlarm.Area = "HMI_RT_1::Alarming";
                    discreteAlarm.RaisedStateTagBitNumber = 0;
                    discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
                }
            }
        }

        private static void DiscreteAlarmCreateTabTCP(Project project, String area, String tab)
        {
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiDiscreteAlarmComposition discreteAlarms = hmiSoftware.DiscreteAlarms;
            String[] alarmTypesTCP = new string[]
            {
                "Alarma_Temp_TCP","Alarma_Armonicos_V_L1_TCP","Alarma_Armonicos_V_L2_TCP","Alarma_Armonicos_V_L3_TCP",
                "Alarma_Voltaje_L1L2_TCP","Alarma_Voltaje_L2L3_TCP","Alarma_Voltaje_L3L1_TCP","Alarma_Cos_L1_TCP",
                "Alarma_Cos_L2_TCP","Alarma_Cos_L3_TCP","Alarma_Armonicos_I_L1_TCP","Alarma_Armonicos_I_L2_TCP",
                "Alarma_Armonicos_I_L3_TCP","Alarma_Energia_Inductiva","Alarma_Energia_Capacitiva_TCP",
                "Alarma_Sobrecompensacion_TCP","Alarma_Subcompensacion_TCP","Comunic_Modbus"
            };
            uint[] index = new uint[]
            {
                0,10,11,12,13,14,15,26,27,28,3,4,5,10,11,12,13,0
            };
            string[] alarmTextTCP = new string[]
            {
                "Alarma Temperatura TCP ", "Alarma Armonicos Voltaje Fase L1 TCP ", "Alarma Armonicos Voltaje Fase L2 TCP ",
                "Alarma Armonicos Voltaje Fase L3 TCP ", "Alarma Voltaje L1L2 TCP ","Alarma Voltaje L2L3 TCP ", "Alarma Voltaje L3L1 TCP ",
                "Alarma Cos Fase L1 TCP ", "Alarma Cos Fase L2 TCP ","Alarma Cos Fase L3 TCP ", "Alarma Armonicos Corriente Fase L3 TCP ",
                "Alarma Armonicos Corriente Fase L2 TCP ", "Alarma Armonicos Corriente Fase L3 TCP ", "Alarma Energía Inductiva TCP ",
                "Alarma Energía Capacitiva TCP ", "Alarma Sobrecompensacion TCP ", "Alarma Subcompensacion TCP ","Alarma Perdida Comun Modbus "
            };

            for (uint i = 1; i <= 12; i++)
            {
                HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + "Pasos_Config_TCP" + "_" + tab + "_" + i);
                discreteAlarm.EventText.Items[0].Text = "<body><p>Paso Configurado TCP " + tab + "_" + i + "</p></body>";
                discreteAlarm.Area = "HMI_RT_1::Alarming";
                discreteAlarm.AlarmClass = area + "_Operacion";
                discreteAlarm.RaisedStateTag = tab + ".Pasos_Habilit";
                discreteAlarm.RaisedStateTagBitNumber = i - 1;
                discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;

                HmiDiscreteAlarm discreteAlarm1 = discreteAlarms.Create(area + "_" + "Pasos_Activos_TCP" + "_" + tab + "_" + i);
                discreteAlarm1.EventText.Items[0].Text = "<body><p>Paso Activado TCP " + tab + "_" + i + "</p></body>";
                discreteAlarm1.Area = "HMI_RT_1::Alarming";
                discreteAlarm1.AlarmClass = area + "_Operacion";
                discreteAlarm1.RaisedStateTag = tab + ".Pasos_Acti";
                discreteAlarm1.RaisedStateTagBitNumber = i - 1;
                discreteAlarm1.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
            }

            for (int i = 0; i < alarmTypesTCP.Length; i++)
            {
                HmiDiscreteAlarm discreteAlarm = discreteAlarms.Create(area + "_" + alarmTypesTCP[i] + "_" + tab);
                discreteAlarm.EventText.Items[0].Text = "<body><p>" + alarmTextTCP[i] + tab + "</p></body>";
                discreteAlarm.Area = "HMI_RT_1::Alarming";
                if (i < 10)
                {
                    discreteAlarm.AlarmClass = area + "_Alarmas";
                    discreteAlarm.RaisedStateTag = tab + ".Alarm1";
                }
                else if (i >= 10 && !alarmTypesTCP[i].Equals("Comunic_Modbus"))
                {
                    discreteAlarm.AlarmClass = area + "_Alarmas";
                    discreteAlarm.RaisedStateTag = tab + ".Alarm2";
                }
                else if (alarmTypesTCP[i].Equals("Comunic_Modbus"))
                {
                    discreteAlarm.AlarmClass = area + "_Comunicaciones";
                    discreteAlarm.RaisedStateTag = tab + ".Status_Comm_MB";
                }
                discreteAlarm.RaisedStateTagBitNumber = index[i];
                discreteAlarm.TriggerMode = HmiDiscreteAlarmTriggerMode.OnRisingEdge;
            }
        }

        private static void AnalogAlarmCreate(Project project, String area, String tab)
        {
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiAnalogAlarmComposition analogAlarms = hmiSoftware.AnalogAlarms;

            // Modificación: El tab (ej: 1B1_TA01) solo tiene 2 partes
            string[] res = tab.Split('_');

            //Antenas
            HmiAnalogAlarm analogAlarm = analogAlarms.Create(area + "_Comunic_ANT_" + tab);
            analogAlarm.EventText.Items[0].Text = "<body><p>Alarma Perdida Comun ANT " + tab + "</p></body>";
            analogAlarm.RaisedStateTag = "Ant_Pot_" +res[0] +"_" + res[1]; // Ajustado a 2 partes
            analogAlarm.Condition = HmiAlarmCondition.LowerLimitOrEqual;
            analogAlarm.ConditionValue = -87;
            analogAlarm.AlarmClass = area + "_Comunicaciones";
            analogAlarm.Area = "HMI_RT_1::Alarming";

            //Selector Manual
            HmiAnalogAlarm analogAlarm1 = analogAlarms.Create(area + "_Selector_Local_Manual_" + tab);
            analogAlarm1.EventText.Items[0].Text = "<body><p>Operacion Manual " + tab + "</p></body>";
            analogAlarm1.RaisedStateTag = "Op_Local_" + res[0] +"_"+ res[1]; // Ajustado a 2 partes
            analogAlarm1.Condition = HmiAlarmCondition.Equal;
            analogAlarm1.ConditionValue = 1;
            analogAlarm1.AlarmClass = area + "_Operacion";
            analogAlarm1.Area = "HMI_RT_1::Alarming";

            //Selector Automatico
            HmiAnalogAlarm analogAlarm2 = analogAlarms.Create(area + "_Selector_Local_Automatico_" + tab);
            analogAlarm2.EventText.Items[0].Text = "<body><p>Operacion Automatico " + tab + "</p></body>";
            analogAlarm2.RaisedStateTag = "Op_Local_" + res[0] + "_" + res[1]; // Ajustado a 2 partes
            analogAlarm2.Condition = HmiAlarmCondition.Equal;
            analogAlarm2.ConditionValue = 2;
            analogAlarm2.AlarmClass = area + "_Operacion";
            analogAlarm2.Area = "HMI_RT_1::Alarming";

            //Selector Off
            HmiAnalogAlarm analogAlarm3 = analogAlarms.Create(area + "_Selector_Local_Off_" + tab);
            analogAlarm3.EventText.Items[0].Text = "<body><p>Operacion Off " + tab + "</p></body>";
            analogAlarm3.RaisedStateTag = "Op_Local_" + res[0] + "_" + res[1]; // Ajustado a 2 partes
            analogAlarm3.Condition = HmiAlarmCondition.Equal;
            analogAlarm3.ConditionValue = 0;
            analogAlarm3.AlarmClass = area + "_Operacion";
            analogAlarm3.Area = "HMI_RT_1::Alarming";
        }

        private static void TagTableCreate(Project project, String nameTagTable)
        {
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiTagTableComposition tagTables = hmiSoftware.TagTables;
            HmiTagTable hmiTagTable = tagTables.Create(nameTagTable);
        }

        private static void AlarmClassCreate(Project project, String nameAlarmClass)
        {
            HmiSoftware hmiSoftware = GetHmiSoftware(project);
            HmiAlarmClassComposition alarmClasses = hmiSoftware.AlarmClasses;
            HmiAlarmClass alarmClass = alarmClasses.Create("AlarmClass_1");
        }

        private static Dictionary<string, PiscinasData> CargarDatos()
        {
            // Json actualizado con los nuevos datos y estructura sin el prefijo de sector
            string json = @"{
  ""TAURAI1"": {
    ""TAURAI1_TA01"": {
      ""NameTCP"": ""TCP001-PS001/PS002"",
      ""CantCirc"": ""8""
    }
  },
  ""TAURAI100"": {
    ""TAURAI100_TA01"": {
      ""NameTCP"": ""TCP001-PS100"",
      ""CantCirc"": ""7""
    },
    ""TAURAI100_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAI119"": {
    ""TAURAI119_TA01"": {
      ""NameTCP"": ""TCP001-PS119/PSX11"",
      ""CantCirc"": ""6""
    },
    ""TAURAI119_TA02"": {
      ""NameTCP"": ""TCP002-PS119/PSX11"",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAI13"": {
    ""TAURAI13_TA01"": {
      ""NameTCP"": ""TCP001-PS013/PS015"",
      ""CantCirc"": ""6""
    },
    ""TAURAI13_TA02"": {
      ""NameTCP"": ""TCP002-PS013/PS015"",
      ""CantCirc"": ""5""
    }
  },
  ""TAURAI15"": {
    ""TAURAI15_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    },
    ""TAURAI15_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    }
  },
  ""TAURAI2"": {
    ""TAURAI2_TA01"": {
      ""NameTCP"": ""TCP001-PS002"",
      ""CantCirc"": ""5""
    },
    ""TAURAI2_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""5""
    }
  },
  ""TAURAI3"": {
    ""TAURAI3_TA01"": {
      ""NameTCP"": ""TCP001-PS003/PS004"",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAI33"": {
    ""TAURAI33_TA01"": {
      ""NameTCP"": ""TCP001-PS033"",
      ""CantCirc"": ""7""
    },
    ""TAURAI33_TA02"": {
      ""NameTCP"": ""TCP002-PS033"",
      ""CantCirc"": ""6""
    }
  },
  ""TAURAI34"": {
    ""TAURAI34_TA01"": {
      ""NameTCP"": ""TCP001-PS034/PS0X7"",
      ""CantCirc"": ""5""
    },
    ""TAURAI34_TA02"": {
      ""NameTCP"": ""TCP002-PS034/PS0X7"",
      ""CantCirc"": ""5""
    }
  },
  ""TAURAI4"": {
    ""TAURAI4_TA01"": {
      ""NameTCP"": ""TCP001-PS004"",
      ""CantCirc"": ""4""
    },
    ""TAURAI4_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""5""
    }
  },
  ""TAURAI5"": {
    ""TAURAI5_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    }
  },
  ""TAURAI53"": {
    ""TAURAI53_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    },
    ""TAURAI53_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    }
  },
  ""TAURAI6"": {
    ""TAURAI6_TA01"": {
      ""NameTCP"": ""TCP001-PS006/PS005"",
      ""CantCirc"": ""6""
    }
  },
  ""TAURAI63"": {
    ""TAURAI63_TA01"": {
      ""NameTCP"": ""TCP001-PS063/PS067"",
      ""CantCirc"": ""5""
    },
    ""TAURAI63_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""4""
    }
  },
  ""TAURAI67"": {
    ""TAURAI67_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    },
    ""TAURAI67_TA02"": {
      ""NameTCP"": ""TCP002-PS063/PS067"",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAI69"": {
    ""TAURAI69_TA01"": {
      ""NameTCP"": ""TCP001-PS069"",
      ""CantCirc"": ""7""
    },
    ""TAURAI69_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    },
    ""TAURAI69_TA03"": {
      ""NameTCP"": ""TCP002-PS069"",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAI7"": {
    ""TAURAI7_TA01"": {
      ""NameTCP"": ""TCP001-PS007"",
      ""CantCirc"": ""8""
    }
  },
  ""TAURAI70"": {
    ""TAURAI70_TA01"": {
      ""NameTCP"": ""TCP001-PS070"",
      ""CantCirc"": ""7""
    },
    ""TAURAI70_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    },
    ""TAURAI70_TA03"": {
      ""NameTCP"": ""TCP002-PS070"",
      ""CantCirc"": ""6""
    }
  },
  ""TAURAI71"": {
    ""TAURAI71_TA01"": {
      ""NameTCP"": ""TCP001-PS071/PS072"",
      ""CantCirc"": ""6""
    },
    ""TAURAI71_TA02"": {
      ""NameTCP"": ""TCP002-PS071/PS072"",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAI72"": {
    ""TAURAI72_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    },
    ""TAURAI72_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    }
  },
  ""TAURAI73"": {
    ""TAURAI73_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""5""
    },
    ""TAURAI73_TA02"": {
      ""NameTCP"": ""TCP002-PS074/PS073"",
      ""CantCirc"": ""5""
    }
  },
  ""TAURAI74"": {
    ""TAURAI74_TA01"": {
      ""NameTCP"": ""TCP001-PS074/PS073"",
      ""CantCirc"": ""8""
    },
    ""TAURAI74_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""8""
    }
  },
  ""TAURAI75"": {
    ""TAURAI75_TA01"": {
      ""NameTCP"": ""TCP001-PS075"",
      ""CantCirc"": ""6""
    },
    ""TAURAI75_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""8""
    },
    ""TAURAI75_TA03"": {
      ""NameTCP"": ""TCP002-PS075"",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAI9"": {
    ""TAURAI9_TA01"": {
      ""NameTCP"": ""TCP001-PS009"",
      ""CantCirc"": ""8""
    }
  },
  ""TAURAIX1"": {
    ""TAURAIX1_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAIX11"": {
    ""TAURAIX11_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""7""
    },
    ""TAURAIX11_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    }
  },
  ""TAURAIX2"": {
    ""TAURAIX2_TA01"": {
      ""NameTCP"": ""TCP001-PR0X2/PR0X1"",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAIX3"": {
    ""TAURAIX3_TA01"": {
      ""NameTCP"": ""TCP001-PR0X3/PR0X4"",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAIX4"": {
    ""TAURAIX4_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAIX7"": {
    ""TAURAIX7_TA01"": {
      ""NameTCP"": """",
      ""CantCirc"": ""7""
    },
    ""TAURAIX7_TA02"": {
      ""NameTCP"": """",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAIX8"": {
    ""TAURAIX8_TA01"": {
      ""NameTCP"": ""TCP001-PS0X8"",
      ""CantCirc"": ""7""
    },
    ""TAURAIX8_TA02"": {
      ""NameTCP"": ""TCP002-PS0X8"",
      ""CantCirc"": ""7""
    },
    ""TAURAIX8_TA03"": {
      ""NameTCP"": """",
      ""CantCirc"": ""6""
    },
    ""TAURAIX8_TA04"": {
      ""NameTCP"": ""TCP003-PS0X8"",
      ""CantCirc"": ""7""
    },
    ""TAURAIX8_TA05"": {
      ""NameTCP"": ""TCP004-PS0X8"",
      ""CantCirc"": ""7""
    }
  },
  ""TAURAIX9"": {
    ""TAURAIX9_TA01"": {
      ""NameTCP"": ""TCP001-PS0X9/PS053"",
      ""CantCirc"": ""7""
    },
    ""TAURAIX9_TA02"": {
      ""NameTCP"": ""TCP002-PS0X9/PS053"",
      ""CantCirc"": ""7""
    },
    ""TAURAIX9_TA03"": {
      ""NameTCP"": ""TCP001-PS0X9/PS0X8"",
      ""CantCirc"": ""8""
    }
  }
}";
            return JsonConvert.DeserializeObject<Dictionary<string, PiscinasData>>(json);
        }
    }

    public class PiscinaData
    {
        public string NameTCP { get; set; }
        public string CantCirc { get; set; }
    }

    public class PiscinasData : Dictionary<string, PiscinaData> { }

}