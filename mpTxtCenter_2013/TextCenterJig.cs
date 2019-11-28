namespace mpTxtCenter
{
    using Autodesk.AutoCAD.ApplicationServices.Core;
    using Autodesk.AutoCAD.DatabaseServices;
    using Autodesk.AutoCAD.EditorInput;
    using Autodesk.AutoCAD.Geometry;
    using Autodesk.AutoCAD.GraphicsInterface;
    using ModPlusAPI;

    public class TextCenterJig : DrawJig
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

        public PromptResult StartJig(string str, Point3d fPt)
        {
            _prevPoint = new Point3d(0, 0, 0);
            _startPoint = fPt;
            _line = new Line { StartPoint = fPt };
            _txt = new DBText();
            _txt.SetDatabaseDefaults();
            _txt.TextString = str;
            _txt.Justify = AttachmentPoint.MiddleCenter;
            return Application.DocumentManager.MdiActiveDocument.Editor.Drag(this);
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
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