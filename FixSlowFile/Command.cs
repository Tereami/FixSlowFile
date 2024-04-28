﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace FixSlowFile
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Trace.Listeners.Clear();
            Trace.Listeners.Add(new RbsLogger.Logger("FixSlowFile"));
            string startTime = DateTime.Now.ToLongTimeString();
            Trace.WriteLine("Start time " + startTime);
            Document doc = commandData.Application.ActiveUIDocument.Document;

            //получить все типы арматуры
            List<RebarBarType> rebarTypes = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .ToList();
            Trace.WriteLine("Rebar types found: " + rebarTypes.Count.ToString());
            if(rebarTypes.Count == 0)
            {
                TaskDialog.Show("Error", "В файле нет типов арматурных стержней");
                return Result.Failed;
            }

            //посмотрю, какие общие параметры проекта добавлены для типа арматуры
            Dictionary<string, MyProjectSharedParameter> projectParamsStorage = new Dictionary<string, MyProjectSharedParameter>();
            RebarBarType firstBarType = rebarTypes.First();
            foreach (Parameter param in firstBarType.ParametersMap)
            {
                string paramName = param.Definition.Name;
                if (!param.IsShared) continue;
                MyProjectSharedParameter mpsp = new MyProjectSharedParameter(param, doc);
                projectParamsStorage.Add(paramName, mpsp);
                Trace.WriteLine("Shared parameter found: " + paramName);
            }

            //запоминаю все типы арматуры со значениями параметров
            List<MyRebarType> myrebarTypes = new List<MyRebarType>();

            foreach (RebarBarType rbt in rebarTypes)
            {
                MyRebarType mrt = new MyRebarType(rbt);
                myrebarTypes.Add(mrt);
                Trace.WriteLine("Rebat type saved: " + mrt.bartype.Name);
            }


            DefinitionFile deffile = null;
            try
            {
                deffile = commandData.Application.Application.OpenSharedParameterFile();
            }
            catch
            {
                TaskDialog.Show("Ошибка", "Не найден файл общих параметров");
                Trace.WriteLine("Shared parameters file isnt found");
                return Result.Cancelled;
            }

            if (deffile == null)
            {
                TaskDialog.Show("Ошибка", "Некорректный файл общих параметров");
                Trace.WriteLine("Shared parameters file is incorrect");
                return Result.Cancelled;
            }



            //удаляю параметр проекта (если только 1 категория) или снимаю флажок с категории несущей арматуры (если категорий несколько)
            using (Transaction t = new Transaction(doc))
            {
                Trace.WriteLine("Start clear parameters");
                t.Start("Удаляю параметры несущей арматуры");
                {
                    foreach (var kvp in projectParamsStorage)
                    {
                        MyProjectSharedParameter myProjectParam = kvp.Value;
                        if (myProjectParam.categories.Count == 1)
                        {
                            //параметр только для несущей арматуры, значит надо удалить целиком
                            //перед этим проверяю, есть ли параметр в фопе
                            
                            bool checkParamExistsInDefFile = SharedParamsFileTools.CheckParameterExistsInFile(deffile, myProjectParam.guid);
                            if (!checkParamExistsInDefFile)
                            {
                                SharedParamsFileTools.AddParameterToDefFile(deffile, "NonTemplate parameters", myProjectParam);
                            }


                            doc.ParameterBindings.Remove(myProjectParam.def);
                            Trace.WriteLine("Parameter is deleted: " + myProjectParam.Name);
                        }
                        else
                        {
                            //категорий несколько, надо убрать флажок с категории несущей арматуры
                            myProjectParam.RemoveOrAddFromRebarCategory(doc, firstBarType, false);
                            Trace.WriteLine("Flag for rebars deleted for parameter: " + myProjectParam.Name);
                        }
                    }
                }
                t.Commit();
            }


            Trace.WriteLine("All parameters are deleted, go to recover");

            //возвращаю параметры обратно
            using (Transaction t2 = new Transaction(doc))
            {
                t2.Start("Добавляю параметры обратно");

                foreach (var kvp in projectParamsStorage)
                {
                    MyProjectSharedParameter myProjectParam = kvp.Value;
                    if (myProjectParam.categories.Count == 1)
                    {
                        //параметр был несущей арматуры, был удален совсем, значит создаю параметр
                        myProjectParam.AddToProjectParameters(doc, firstBarType);
                        Trace.WriteLine("New parameter is created: " + myProjectParam.Name);
                    }
                    else
                    {
                        //категорий было несколько, возвращаю флажок к категории несущей арматуры
                        myProjectParam.RemoveOrAddFromRebarCategory(doc, firstBarType, true);
                        Trace.WriteLine("Flag recovered for parameter: " + myProjectParam.Name);
                    }
                }

                t2.Commit();
            }

            Trace.WriteLine("Start recover parameter values");
            //восстанавливаю значения у типов арматуры
            using (Transaction t3 = new Transaction(doc))
            {
                t3.Start("Восстанавливаю значения параметров");

                foreach (MyRebarType mrt in myrebarTypes)
                {
                    RebarBarType rbt = mrt.bartype;
                    Trace.WriteLine("Processed rebar type: " + mrt.Name);

                    foreach (Parameter param in rbt.ParametersMap)
                    {
                        string paramName = param.Definition.Name;
                        MyParameterValue mpv = mrt.ValuesStorage[paramName];
                        if (mpv.IsNull) continue;
                        mpv.SetValue(param);
                        Trace.WriteLine("Parameter " + paramName + ", set value " + mpv.ToString());
                    }
                }

                t3.Commit();
            }
            string endTime = DateTime.Now.ToLongTimeString();
            string msg = "Выполнено! Время старта: " + startTime + ", окончания: " + endTime;

            TaskDialog.Show("Fix", msg);
            Trace.WriteLine(msg);

            return Result.Succeeded;
        }
    }
}
