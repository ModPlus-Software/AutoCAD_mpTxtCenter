#if ac2010
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
#elif ac2013
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;
#endif
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using ModPlus;
using mpMsg;

namespace mpTxtCenter
{
    public class MpTxtCenter
    {
        [CommandMethod("ModPlus", "mpTxtCenter", CommandFlags.Modal)]
        public static void Main()
        {
            try
            {
                var keepLoopin = true;
                var pt = new Point3d();
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var db = doc.Database;
                while (true)
                {
                    var ppo = new PromptPointOptions(string.Empty);
                    while (keepLoopin)
                    {
                        ppo.SetMessageAndKeywords("\nУкажите первую точку или [Существующий]", "Существующий");
                        ppo.AppendKeywordsToMessage = true;
                        var ppr = ed.GetPoint(ppo);
                        switch (ppr.Status)
                        {
                            case PromptStatus.Keyword:
                                MpTxtCenterExist();
                                break;
                            case PromptStatus.Cancel:
                                return;
                            case PromptStatus.Error:
                                return;
                            case PromptStatus.None:
                                return;
                            case PromptStatus.Other:
                                return;
                            case PromptStatus.OK:
                                pt = ppr.Value;
                                keepLoopin = false;
                                break;
                        }
                    }
                    keepLoopin = true;
                    var pso =
                        new PromptStringOptions("\nВведите текст: ") {AllowSpaces = true};
                    var psr = ed.GetString(pso);
                    if (psr.Status != PromptStatus.OK || psr.StringResult == string.Empty)
                    {
                        return;
                    } // Если не ОК, тогда завершить


                    using (var tr = db.TransactionManager.StartTransaction())
                    {
                        var fPt = MpCadHelpers.UcsToWcs(pt);
                        var btr = (BlockTableRecord) tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite, false);
                        var jig = new MpTxtCenterJig();
                        var rs = jig.StartJig(psr.StringResult, fPt);
                        if (rs.Status == PromptStatus.OK) // Если джига отработала хорошо
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
                } // while
            } // try
            catch (System.Exception ex)
            {
                MpExWin.Show(ex);
            }
        }

        static private void MpTxtCenterExist()
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                var ed = doc.Editor;
                var db = doc.Database;

                var entOpt = new PromptEntityOptions("\nВыберите однострочный текст: ");
                entOpt.SetRejectMessage("\nНеверный выбор!");
                entOpt.AddAllowedClass(typeof(DBText), false);
                var entRes = ed.GetEntity(entOpt);
                if (entRes.Status != PromptStatus.OK) return;

                var pointOpt = new PromptPointOptions("\nУкажите первую точку: ");
                var pointRes = ed.GetPoint(pointOpt);
                if (pointRes.Status != PromptStatus.OK) return;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var fPt = MpCadHelpers.UcsToWcs(pointRes.Value);
                    var txt = (DBText) tr.GetObject(entRes.ObjectId, OpenMode.ForWrite);
                    txt.Justify = AttachmentPoint.MiddleCenter;
                    var jig = new MpTxtCenterJigExist();
                    var rs = jig.StartJig(txt, fPt);
                    if (rs.Status != PromptStatus.OK) return;
                    tr.Commit();
                }
            } // try
            catch (System.Exception ex)
            {
                MpExWin.Show(ex);
            }
        }
    }

    public class MpTxtCenterJig : DrawJig
    {
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
            _line = new Line {StartPoint = fPt};
            _txt = new DBText();
            _txt.SetDatabaseDefaults();
            _txt.TextString = str;
            _txt.Justify = AttachmentPoint.MiddleCenter;
            return AcApp.DocumentManager.MdiActiveDocument.Editor.Drag(this);
        } // public AcEd.PromptResult StartJig(string str)

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var jppo = new JigPromptPointOptions("\nУкажите вторую точку: ")
            {
                BasePoint = _startPoint,
                UseBasePoint = true,
                UserInputControls = (UserInputControls.Accept3dCoordinates
                                     | UserInputControls.NoZeroResponseAccepted
                                     | UserInputControls.AcceptOtherInputString
                                     | UserInputControls.NoNegativeResponseAccepted)
            };
            var rs = prompts.AcquirePoint(jppo);
            _currPoint = rs.Value;
            if (rs.Status != PromptStatus.OK) return SamplerStatus.Cancel;
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
            _middlePt = new Point3d((_line.StartPoint.X + _currPoint.X) / 2,
                (_line.StartPoint.Y + _currPoint.Y) / 2,
                (_line.StartPoint.Z + _currPoint.Z) / 2);
            _txt.AlignmentPoint = _middlePt;
            _txt.Position =
                new Point3d(
                    _txt.AlignmentPoint.X - (_txt.GeometricExtents.MaxPoint.X - _txt.GeometricExtents.MinPoint.X) / 2,
                    _txt.AlignmentPoint.Y - (_txt.GeometricExtents.MaxPoint.Y - _txt.GeometricExtents.MinPoint.Y) / 2,
                    _txt.AlignmentPoint.Z - (_txt.GeometricExtents.MaxPoint.Z - _txt.GeometricExtents.MinPoint.Z) / 2);

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
    } // public class MpTxtCenterJig : AcEd.DrawJig

    public class MpTxtCenterJigExist : DrawJig
    {
        private Point3d _prevPoint; // Предыдущая точка
        private Point3d _currPoint; // Нинешняя точка
        private Point3d _startPoint;
        private DBText _txt; // Текстовый объект
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
            _line = new Line {StartPoint = fPt};
            _txt = dbtext;
            return AcApp.DocumentManager.MdiActiveDocument.Editor.Drag(this);
        } // public AcEd.PromptResult StartJig(string str)

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            var jppo = new JigPromptPointOptions("\nУкажите вторую точку: ")
            {
                BasePoint = _startPoint,
                UseBasePoint = true,
                UserInputControls = (UserInputControls.Accept3dCoordinates
                                     | UserInputControls.NoZeroResponseAccepted
                                     | UserInputControls.AcceptOtherInputString
                                     | UserInputControls.NoNegativeResponseAccepted)
            };
            var rs = prompts.AcquirePoint(jppo);
            _currPoint = rs.Value;
            if (rs.Status != PromptStatus.OK) return SamplerStatus.Cancel;
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
            _middlePt = new Point3d((_line.StartPoint.X + _currPoint.X) / 2,
                (_line.StartPoint.Y + _currPoint.Y) / 2,
                (_line.StartPoint.Z + _currPoint.Z) / 2);
            _txt.AlignmentPoint = _middlePt;
            _txt.Position =
                new Point3d(
                    _txt.AlignmentPoint.X - (_txt.GeometricExtents.MaxPoint.X - _txt.GeometricExtents.MinPoint.X) / 2,
                    _txt.AlignmentPoint.Y - (_txt.GeometricExtents.MaxPoint.Y - _txt.GeometricExtents.MinPoint.Y) / 2,
                    _txt.AlignmentPoint.Z - (_txt.GeometricExtents.MaxPoint.Z - _txt.GeometricExtents.MinPoint.Z) / 2);

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
    } // public class MpTxtCenterJig : AcEd.DrawJig
}