namespace mpTxtCenter
{
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.GraphicsInterface;
    using Autodesk.AutoCAD.Runtime;
    using ModPlusAPI;
    using ModPlusAPI.Windows;
    using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

    public class TextCenter
    {
        private const string LangItem = "mpTxtCenter";

        [CommandMethod("ModPlus", "mpTxtCenter", CommandFlags.Modal | CommandFlags.UsePickSet)]
        public static void Main()
        {
            Statistic.SendCommandStarting(new ModPlusConnector());
            
            var workVariant =
                UserConfigFile.GetValue(UserConfigFile.ConfigFileZone.Settings, "mpTxtCenter", "WorkVariant");
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
                        ppo.SetMessageAndKeywords("\n" + Language.GetItem(LangItem, "msg1"), Language.GetItem(LangItem, "msg2"));
                        ppo.AppendKeywordsToMessage = true;
                        var ppr = ed.GetPoint(ppo);
                        switch (ppr.Status)
                        {
                            case PromptStatus.Keyword:
                                UserConfigFile.SetValue(UserConfigFile.ConfigFileZone.Settings, "mpTxtCenter", "WorkVariant", "Exist", true);
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
                    var pso = new PromptStringOptions("\n" + Language.GetItem(LangItem, "msg3")) { AllowSpaces = true };
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
                    PromptSelectionResult selected = ed.SelectImplied();
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
                        var entOpt = new PromptEntityOptions("\n" + Language.GetItem(LangItem, "msg4"));
                        entOpt.SetMessageAndKeywords("\n" + Language.GetItem(LangItem, "msg5"), Language.GetItem(LangItem, "msg6"));
                        entOpt.SetRejectMessage("\n" + Language.GetItem(LangItem, "msg7"));
                        entOpt.AddAllowedClass(typeof(DBText), false);
                        entRes = ed.GetEntity(entOpt);
                        if (entRes.Status == PromptStatus.Keyword)
                        {
                            UserConfigFile.SetValue(UserConfigFile.ConfigFileZone.Settings, "mpTxtCenter",
                                "WorkVariant", "New", true);
                            MpTxtCenterNew();
                            break;
                        }

                        if (entRes.Status != PromptStatus.OK)
                            return;
                    }

                    var pointOpt = new PromptPointOptions("\n" + Language.GetItem(LangItem, "msg8"));
                    var pointRes = ed.GetPoint(pointOpt);
                    if (pointRes.Status != PromptStatus.OK)
                        return;
                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var fPt = ModPlus.Helpers.AutocadHelpers.UcsToWcs(pointRes.Value);
                        if (preSelectTxt != null)
                        {
                            DBText txt = (DBText) tr.GetObject(preSelectTxt.ObjectId, OpenMode.ForWrite);
                            txt.Justify = AttachmentPoint.MiddleCenter;
                            var jig = new ExistTextCenterJig();
                            var rs = jig.StartJig(txt, fPt);
                            if (rs.Status != PromptStatus.OK)
                                return;
                            preSelectTxt = null;
                        }
                        else
                        {
                            DBText txt = (DBText) tr.GetObject(entRes.ObjectId, OpenMode.ForWrite);
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

    public class TextCenterJig : DrawJig
    {
        private const string LangItem = "mpTxtCenter";
        private Point3d _prevPoint; // Предыдущая точка
        private Point3d _currPoint; // Нынешняя точка
        private Point3d _startPoint;
        private DBText _txt; // Текстовый объект
        private Line _line;
        private Point3d _middlePt;

        public Point3d Point()
        {
            return _middlePt;
        }

        public PromptResult StartJig(string str, Point3d fPt)
        {
            _prevPoint = new Point3d(0, 0, 0);
            _startPoint = fPt;
            _line = new Line { StartPoint = fPt };
            _txt = new DBText();
            _txt.SetDatabaseDefaults();
            _txt.TextString = str;
            _txt.Justify = AttachmentPoint.MiddleCenter;
            return AcApp.DocumentManager.MdiActiveDocument.Editor.Drag(this);
        } // public AcEd.PromptResult StartJig(string str)

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var jppo = new JigPromptPointOptions("\n" + Language.GetItem(LangItem, "msg9"))
            {
                BasePoint = _startPoint,
                UseBasePoint = true,
                UserInputControls = 
                    UserInputControls.Accept3dCoordinates |
                    UserInputControls.NoZeroResponseAccepted |
                    UserInputControls.AcceptOtherInputString |
                    UserInputControls.NoNegativeResponseAccepted
            };
            var rs = prompts.AcquirePoint(jppo);
            _currPoint = rs.Value;
            if (rs.Status != PromptStatus.OK)
                return SamplerStatus.Cancel;
            if (CursorHasMoved())
            {
                var displacementVector = _prevPoint.GetVectorTo(_currPoint);
                _txt.TransformBy(Matrix3d.Displacement(displacementVector));
                _prevPoint = _currPoint;
                return SamplerStatus.OK;
            }

            return SamplerStatus.NoChange;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            _middlePt = new Point3d(
                (_line.StartPoint.X + _currPoint.X) / 2,
                (_line.StartPoint.Y + _currPoint.Y) / 2,
                (_line.StartPoint.Z + _currPoint.Z) / 2);
            _txt.AlignmentPoint = _middlePt;
            _txt.Position =
                new Point3d(
                    _txt.AlignmentPoint.X - ((_txt.GeometricExtents.MaxPoint.X - _txt.GeometricExtents.MinPoint.X) / 2),
                    _txt.AlignmentPoint.Y - ((_txt.GeometricExtents.MaxPoint.Y - _txt.GeometricExtents.MinPoint.Y) / 2),
                    _txt.AlignmentPoint.Z - ((_txt.GeometricExtents.MaxPoint.Z - _txt.GeometricExtents.MinPoint.Z) / 2));

            _line.StartPoint = _startPoint;
            _line.EndPoint = _currPoint;
            draw.Geometry.Draw(_txt);
            draw.Geometry.Draw(_line);
            return true;
        }

        private bool CursorHasMoved()
        {
            return _currPoint.DistanceTo(_prevPoint) > 1e-6;
        }
    }

    public class ExistTextCenterJig : DrawJig
    {
        private const string LangItem = "mpTxtCenter";
        private Point3d _prevPoint;
        private Point3d _currentPoint;
        private Point3d _startPoint;
        private DBText _txt;
        private Line _line;
        private Point3d _middlePt;

        public Point3d Point()
        {
            return _middlePt;
        }

        public PromptResult StartJig(DBText dbtext, Point3d fPt)
        {
            _prevPoint = new Point3d(0, 0, 0);
            _startPoint = fPt;
            _line = new Line { StartPoint = fPt };
            _txt = dbtext;
            return AcApp.DocumentManager.MdiActiveDocument.Editor.Drag(this);
        } // public AcEd.PromptResult StartJig(string str)

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var jigPromptPointOptions = new JigPromptPointOptions("\n" + Language.GetItem(LangItem, "msg9"))
            {
                BasePoint = _startPoint,
                UseBasePoint = true,
                UserInputControls = 
                    UserInputControls.Accept3dCoordinates |
                    UserInputControls.NoZeroResponseAccepted |
                    UserInputControls.AcceptOtherInputString |
                    UserInputControls.NoNegativeResponseAccepted
            };
            var rs = prompts.AcquirePoint(jigPromptPointOptions);
            _currentPoint = rs.Value;
            if (rs.Status != PromptStatus.OK)
                return SamplerStatus.Cancel;
            if (CursorHasMoved())
            {
                var displacementVector = _prevPoint.GetVectorTo(_currentPoint);
                _txt.TransformBy(Matrix3d.Displacement(displacementVector));
                _prevPoint = _currentPoint;
                return SamplerStatus.OK;
            }

            return SamplerStatus.NoChange;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            _middlePt = new Point3d(
                (_line.StartPoint.X + _currentPoint.X) / 2,
                (_line.StartPoint.Y + _currentPoint.Y) / 2,
                (_line.StartPoint.Z + _currentPoint.Z) / 2);
            _txt.AlignmentPoint = _middlePt;
            _txt.Position =
                new Point3d(
                    _txt.AlignmentPoint.X - ((_txt.GeometricExtents.MaxPoint.X - _txt.GeometricExtents.MinPoint.X) / 2),
                    _txt.AlignmentPoint.Y - ((_txt.GeometricExtents.MaxPoint.Y - _txt.GeometricExtents.MinPoint.Y) / 2),
                    _txt.AlignmentPoint.Z - ((_txt.GeometricExtents.MaxPoint.Z - _txt.GeometricExtents.MinPoint.Z) / 2));

            _line.StartPoint = _startPoint;
            _line.EndPoint = _currentPoint;
            draw.Geometry.Draw(_txt);
            draw.Geometry.Draw(_line);
            return true;
        }

        private bool CursorHasMoved()
        {
            return _currentPoint.DistanceTo(_prevPoint) > 1e-6;
        }
    }
}