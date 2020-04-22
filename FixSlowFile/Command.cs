﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.DB;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace FixSlowFile
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    class Command :IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            string startTime = DateTime.Now.ToLongTimeString();
            Document doc = commandData.Application.ActiveUIDocument.Document;

            //app.SharedParametersFilename = @"\\picompany.ru\pikp\lib\_CadSettings\02_Revit\04. Shared Parameters\КР\Weandrevit 2017.txt";

            //получить все типы арматуры
            List<RebarBarType> rebarTypes = new FilteredElementCollector(doc)
                .WhereElementIsElementType()
                .OfClass(typeof(RebarBarType))
                .Cast<RebarBarType>()
                .ToList();

            //посмотрю, какие общие параметры проекта добавлены для типа арматуры
            Dictionary<string, MyProjectSharedParameter> projectParamsStorage = new Dictionary<string, MyProjectSharedParameter>();
            RebarBarType firstBarType = rebarTypes.First();
            foreach (Parameter param in firstBarType.ParametersMap)
            {
                string paramName = param.Definition.Name;
                if (!param.IsShared) continue;
                MyProjectSharedParameter mpsp = new MyProjectSharedParameter(param, doc);
                projectParamsStorage.Add(paramName, mpsp);
            }

            //запоминаю все типы арматуры со значениями параметров
            List<MyRebarType> myrebarTypes = new List<MyRebarType>();

            foreach (RebarBarType rbt in rebarTypes)
            {
                MyRebarType mrt = new MyRebarType(rbt);
                myrebarTypes.Add(mrt);
            }


            //удаляю параметр проекта (если только 1 категория) или снимаю флажок с категории несущей арматуры (если категорий несколько)
            using (Transaction t = new Transaction(doc))
            {
                t.Start("Удаляю параметры несущей арматуры");
                {
                    foreach (var kvp in projectParamsStorage)
                    {
                        MyProjectSharedParameter myProjectParam = kvp.Value;
                        if (myProjectParam.categories.Count == 1)
                        {
                            //параметр только для несущей арматуры, значит надо удалить целиком
                            doc.ParameterBindings.Remove(myProjectParam.def);
                        }
                        else
                        {
                            //категорий несколько, надо убрать флажок с категории несущей арматуры
                            myProjectParam.RemoveOrAddFromRebarCategory(doc, firstBarType, false);
                        }
                    }
                }
                t.Commit();
            }




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
                    }
                    else
                    {
                        //категорий было несколько, возвращаю флажок к категории несущей арматуры
                        myProjectParam.RemoveOrAddFromRebarCategory(doc, firstBarType, true);
                    }
                }

                t2.Commit();
            }

            //восстанавливаю значения у типов арматуры
            using (Transaction t3 = new Transaction(doc))
            {
                t3.Start("Восстанавливаю значения параметров");

                foreach (MyRebarType mrt in myrebarTypes)
                {
                    RebarBarType rbt = mrt.bartype;

                    foreach (Parameter param in rbt.ParametersMap)
                    {
                        string paramName = param.Definition.Name;
                        MyParameterValue mpv = mrt.ValuesStorage[paramName];
                        mpv.SetValue(param);
                    }
                }

                t3.Commit();
            }
            string endTime = DateTime.Now.ToLongTimeString();

            TaskDialog.Show("Fix", "Выполнено! Время старта: " + startTime + ", окончания: " + endTime);

            return Result.Succeeded;
        }
    }
}