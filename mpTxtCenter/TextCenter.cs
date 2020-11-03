namespace mpTxtCenter
{
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.Runtime;
    using ModPlusAPI;
    using ModPlusAPI.Windows;
    using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

    public static class TextCenter
    {
        private static readonly string LangItem;

        static TextCenter()
        {
            LangItem = new ModPlusConnector().Name;
        }

        [CommandMethod("ModPlus", "mpTxtCenter", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public static void Main()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            
            var workVariant = UserConfigFile.GetValue(LangItem, "WorkVariant");
            if (workVariant.Equals("Exist"))
                MpTxtCenterExist(true);
            else
                MpTxtCenterNew();
        }

        private static void MpTxtCenterNew()
        {
            try
            {
                var keepLooping = true;
                var pt = default(Point3d);
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var db = doc.Database;
                while (true)
                {
                    var ppo = new PromptPointOptions(string.Empty);
                    while (keepLooping)
                    {
                        ppo.SetMessageAndKeywords($"\n{Language.GetItem("msg1")}", Language.GetItem("msg2"));
                        ppo.AppendKeywordsToMessage = true;
                        var ppr = ed.GetPoint(ppo);
                        switch (ppr.Status)
                        {
                            case PromptStatus.Keyword:
                                UserConfigFile.SetValue(LangItem, "WorkVariant", "Exist", true);
                                MpTxtCenterExist(false);
                                return;
                            case PromptStatus.Error:
                                return;
                            case PromptStatus.None:
                                return;
                            case PromptStatus.Cancel:
                                return;
                            case PromptStatus.Other:
                                return;
                            case PromptStatus.OK:
                                pt = ppr.Value;
                                keepLooping = false;
                                break;
                        }
                    }

                    keepLooping = true;
                    var pso = new PromptStringOptions($"\n{Language.GetItem("msg3")}") { AllowSpaces = true };
                    var psr = ed.GetString(pso);
                    if (psr.Status != PromptStatus.OK || psr.StringResult == string.Empty)
                    {
                        return;
                    }

                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var fPt = ModPlus.Helpers.AutocadHelpers.UcsToWcs(pt);
                        var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);
                        var jig = new TextCenterJig();
                        var rs = jig.StartJig(psr.StringResult, fPt);
                        if (rs.Status == PromptStatus.OK)
                        {
                            var txt = new DBText
                            {
                                TextString = psr.StringResult,
                                TextStyleId = db.Textstyle,
                                Justify = AttachmentPoint.MiddleCenter,
                                Position = jig.Point(),
                                AlignmentPoint = jig.Point()
                            };
                            txt.SetFromStyle();
                            btr.AppendEntity(txt);
                            tr.AddNewlyCreatedDBObject(txt, true);
                            doc.TransactionManager.QueueForGraphicsFlush();
                        }
                        else
                        {
                            tr.Commit();
                            return;
                        }

                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExceptionBox.Show(ex);
            }
        }

        private static void MpTxtCenterExist(bool checkPresSelect)
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var db = doc.Database;
                DBText preSelectTxt = null;
                if (checkPresSelect)
                {
                    var selected = ed.SelectImplied();
                    if (selected.Status == PromptStatus.OK && selected.Value.Count == 1)
                    {
                        foreach (SelectedObject o in selected.Value)
                        {
                            using (var tr = doc.TransactionManager.StartTransaction())
                            {
                                var obj = tr.GetObject(o.ObjectId, OpenMode.ForRead) as DBText;
                                if (obj != null)
                                    preSelectTxt = obj;
                            }
                        }
                    }
                    else
                    {
                        ed.SetImpliedSelection(new ObjectId[0]);
                    }
                }

                while (true)
                {
                    PromptEntityResult entRes = null;
                    if (preSelectTxt == null)
                    {
                        var entOpt = new PromptEntityOptions($"\n{Language.GetItem("msg4")}");
                        entOpt.SetMessageAndKeywords($"\n{Language.GetItem("msg5")}", Language.GetItem("msg6"));
                        entOpt.SetRejectMessage($"\n{Language.GetItem("msg7")}");
                        entOpt.AddAllowedClass(typeof(DBText), false);
                        entRes = ed.GetEntity(entOpt);
                        if (entRes.Status == PromptStatus.Keyword)
                        {
                            UserConfigFile.SetValue(LangItem, "WorkVariant", "New", true);
                            MpTxtCenterNew();
                            break;
                        }

                        if (entRes.Status != PromptStatus.OK)
                            return;
                    }

                    var pointOpt = new PromptPointOptions($"\n{Language.GetItem("msg8")}");
                    var pointRes = ed.GetPoint(pointOpt);
                    if (pointRes.Status != PromptStatus.OK)
                        return;
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var fPt = ModPlus.Helpers.AutocadHelpers.UcsToWcs(pointRes.Value);
                        if (preSelectTxt != null)
                        {
                            var txt = (DBText)tr.GetObject(preSelectTxt.ObjectId, OpenMode.ForWrite);
                            txt.Justify = AttachmentPoint.MiddleCenter;
                            var jig = new ExistTextCenterJig();
                            var rs = jig.StartJig(txt, fPt);
                            if (rs.Status != PromptStatus.OK)
                                return;
                            preSelectTxt = null;
                        }
                        else
                        {
                            var txt = (DBText)tr.GetObject(entRes.ObjectId, OpenMode.ForWrite);
                            txt.Justify = AttachmentPoint.MiddleCenter;
                            var jig = new ExistTextCenterJig();
                            var rs = jig.StartJig(txt, fPt);
                            if (rs.Status != PromptStatus.OK)
                                return;
                        }

                        tr.Commit();
                    }
                }
            }
            catch (System.Exception ex)
            {
                ExceptionBox.Show(ex);
            }
        }
    }
}