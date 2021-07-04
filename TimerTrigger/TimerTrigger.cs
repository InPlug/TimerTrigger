using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using Vishnu.Interchange;

namespace TimerTrigger
{
    /// <summary>
    /// Löst in regelmäßigen, konfigurierbaren Zeitabständen das beim Aufruf der "Start"-Routine
    /// übergebene Event 'triggerIt' aus.
    /// Implementiert die Schnittstelle 'INodeTrigger' aus 'Vishnu.Interchange.dll', über
    /// die sich der LogicalTaskTree von 'Vishnu' in das Event einhängen und den Trigger
    /// starten und stoppen kann.
    /// </summary>
    /// <remarks>
    /// Autor: Erik Nagel
    ///
    /// 19.07.2013 Erik Nagel: erstellt.
    /// 15.12.2020 Erik Nagel: Warte-Durchlauf bei Übergabe von "UserRun" als letztem Parameter implementiert.
    /// 26.06.2021 Erik Nagel: auf neue Basisklasse TriggerBase angepasst.
    /// </remarks>
    public class TimerTrigger : TriggerBase
    {
        #region public members

        #region INodeTrigger Implementation

        /// <summary>
        /// Startet den Trigger; vorher sollte sich der Consumer in TriggerIt eingehängt haben.
        /// </summary>
        /// <param name="triggerController">Das Objekt, das den Trigger aufruft.</param>
        /// <param name="triggerParameters">Zeit bis zum ersten Start und Intervall durch Pipe ('|') getrennt.
        /// Die Zeitangaben bestehen aus Einheit und Wert durch Doppelpunkt getrennt.
        /// Einheiten sind: "MS" Millisekunden, "S" Sekunden, "M" Minuten, "H" Stunden und "D" Tage.</param>
        /// <param name="triggerIt">Die aufzurufende Callback-Routine, wenn der Trigger feuert.</param>
        /// <returns>True, wenn der Trigger durch diesen Aufruf tatsächlich gestartet wurde.</returns>
        public override bool Start(object triggerController, object triggerParameters, Action<TreeEvent> triggerIt)
        {
            base.Start(triggerController, triggerParameters, triggerIt);

            // Trigger-spezifischer Code - Anfang
            if (this._nodeCancellationTokenSource != null)
            {
                this._nodeCancellationTokenSource.Dispose();
            }
            this._nodeCancellationTokenSource = new CancellationTokenSource();
            this._cancellationToken = this._nodeCancellationTokenSource.Token;
            this._cancellationToken.Register(() => CancelNotification());
            this._timerTask = Observable.Timer(this._firstRun, this._interval);
            this._nextStart = DateTime.Now.AddMilliseconds(this._firstRun.TotalMilliseconds);
            this._cancellationToken.Register(this._timerTask.Subscribe(this.OnTriggerFired).Dispose);
            // Trigger-spezifischer Code - Ende

            return true;
        }

        /// <summary>
        /// Stoppt den Trigger.
        /// </summary>
        /// <param name="triggerController">Das Objekt, das den Trigger definiert.</param>
        /// <param name="triggerIt">Die aufzurufende Callback-Routine, wenn der Trigger feuert.</param>
        public override void Stop(object triggerController, Action<TreeEvent> triggerIt)
        {
            if (this._nodeCancellationTokenSource != null)
            {
                this._nodeCancellationTokenSource.Cancel();

                // Weitergabe des Stop-Aufrufs an die Basisklasse
                base.Stop(triggerController, triggerIt);
            }
        }

        #endregion INodeTrigger Implementation

        /// <summary>
        /// Standard Konstruktor.
        /// </summary>
        public TimerTrigger() : base()
        {
            // Optional: Übergabe eines spezifischen Namens (ansonsten wird allgemein "Trigger" gesetzt)
            this.TriggerName = "TimerTrigger";

            // Optional: Formulierung einer Syntaxhilfe (wird im Fehlerfall ausgegeben)
            this._syntaxInformation =
                "Aufrufparameter: [Verzögerung] Intervall"
              + Environment.NewLine
              + "Format von Verzögerung, Interval: Einheit + ':' + ganzzahliger Wert"
              + Environment.NewLine
              + "Einheit: MS=Millisekunden, S=Sekunden, M=Minuten, H=Stunden, D=Tage."
              + Environment.NewLine
              + "Beispiel: TimerTrigger S:5 M:10 (feuert nach einer Wartezeit von 5 Sekunden alle 10 Minuten)";
        }

        #endregion public members

        #region protected members

        /// <summary>
        /// Diese Routine wird von der Routine "Start" angesprungen, bevor der Trigger gestartet wird.
        /// Erweitert TriggerBase.EvaluateParametersOrFail; dort wird nur der Parameter "|UserRun"
        /// ausgewertet und die Variable "_isUserRun" entsprechend gesetzt.
        /// </summary>
        /// <param name="triggerParameters">Die von Vishnu weitergeleiteten Parameter aus der JobDescription.xml.</param>
        /// <param name="triggerController">Der Knoten, dem dieser Trigger zugeordnet ist.</param>
        protected override void EvaluateParametersOrFail(ref object triggerParameters, object triggerController)
        {
            base.EvaluateParametersOrFail(ref triggerParameters, triggerController);

            // Ab hier folgt die Trigger-spezifische Prüfung und Übernahme der von Ihnen über die JobDescription.xml
            // übergebenen Parameter. In %-Zeichen eingeschlossene Zeichenketten wurden ggf. von der
            // Vishnu-Parameterersetzung durch ihre aktuellen Laufzeitwerte ersetzt.
            string firstArg = (triggerParameters.ToString() + "|").Split('|')[0];
            string secondArg = "";
            if (!firstArg.Equals(""))
            {
                secondArg = (triggerParameters.ToString() + "|").Split('|')[1];
            }
            else
            {
                this.ThrowSyntaxException("Es muss zumindest eine Zeitangabe erfolgen.");
            }
            if (String.IsNullOrEmpty(secondArg))
            {
                secondArg = firstArg;
                firstArg = "S:0"; // 0 Sekunden bis zum ersten Trigger-Event
            }
            if (this._isUserRun)
            {
                firstArg = secondArg; // erstmal einen Durchlauf warten, da der Knoten durch UserRun schon direkt gestartet wurde.
            }
            string firstUnity = (firstArg + ":").Split(':')[0].ToUpper();
            if (!(new string[] { "MS", "S", "M", "H", "D" }).Contains(firstUnity))
            {
                this.ThrowSyntaxException("Es wurde keine gültige Einheit (MS, S, M, H, D) angegeben.");
            }
            double firstValue = Convert.ToDouble((firstArg + ":").Split(':')[1]); // wirft eventuell eine Exception, muss dann so sein
            switch (firstUnity)
            {
                case "MS": this._firstRun = TimeSpan.FromMilliseconds(Convert.ToDouble(firstValue)); break;
                case "S": this._firstRun = TimeSpan.FromSeconds(Convert.ToDouble(firstValue)); break;
                case "M": this._firstRun = TimeSpan.FromMinutes(Convert.ToDouble(firstValue)); break;
                case "H": this._firstRun = TimeSpan.FromHours(Convert.ToDouble(firstValue)); break;
                case "D": this._firstRun = TimeSpan.FromDays(Convert.ToDouble(firstValue)); break;
            }
            string secondUnity = (secondArg + ":").Split(':')[0].ToUpper();
            if (!(new string[] { "MS", "S", "M", "H", "D" }).Contains(secondUnity))
            {
                this.ThrowSyntaxException("Es wurde keine gültige Einheit (MS, S, M, H, D) angegeben.");
            }
            double secondValue = Convert.ToDouble((secondArg + ":").Split(':')[1]); // wirft eventuell eine Exception, muss dann so sein
            switch (secondUnity)
            {
                case "MS": this._interval = TimeSpan.FromMilliseconds(Convert.ToDouble(secondValue)); break;
                case "S": this._interval = TimeSpan.FromSeconds(Convert.ToDouble(secondValue)); break;
                case "M": this._interval = TimeSpan.FromMinutes(Convert.ToDouble(secondValue)); break;
                case "H": this._interval = TimeSpan.FromHours(Convert.ToDouble(secondValue)); break;
                case "D": this._interval = TimeSpan.FromDays(Convert.ToDouble(secondValue)); break;
            }
        }

        /// <summary>
        /// Diese Routine löst das Trigger-Event aus.
        /// Für ein Setzen der Variablen "_lastStart" und "_nextStart" überschreibt diese Routine
        /// TriggerBase.OnTriggerFired.
        /// </summary>
        /// <param name="dummy">Aus Kompatibilitätsgründen, wird hier nicht genutzt.</param>
        protected override void OnTriggerFired(long dummy)
        {
            this._lastStart = DateTime.Now;
            this._nextStart = this._lastStart.AddMilliseconds(this._interval.TotalMilliseconds);
            base.OnTriggerFired(dummy);
        }

        #endregion protected members

        #region private members

        private IObservable<long> _timerTask;
        private CancellationTokenSource _nodeCancellationTokenSource { get; set; }
        private CancellationToken _cancellationToken;

        /// <summary>
        /// Definierter Zeitpunkt für den ersten Start
        /// des zugeorneten INodeWorker oder null.
        /// </summary>
        private TimeSpan _firstRun { get; set; }

        /// <summary>
        /// Intervall für den wiederholten Start (Run)
        /// der zugeorneten INodeWorker oder null.
        /// </summary>
        private TimeSpan _interval { get; set; }

        // Informiert über den Abbruch der Verarbeitung.
        private void CancelNotification()
        {
            // noppes
        }

        #endregion private members
    }
}
