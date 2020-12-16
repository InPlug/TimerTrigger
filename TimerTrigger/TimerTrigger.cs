using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Reflection;
using Vishnu.Interchange;

namespace TimerTrigger
{
    /// <summary>
    /// Löst in regelmäßigen, konfigurierbaren Zeitabständen das Event 'TriggerIt'
    /// aus. Implementiert die Schnittstelle 'INodeTrigger' aus 'LogicalTaskTree.dll', über
    /// die sich der LogicalTaskTree von 'Vishnu' in das Event einhängen und den Trigger
    /// starten und stoppen kann.
    /// </summary>
    /// <remarks>
    /// File: TimerTrigger.cs
    /// Autor: Erik Nagel
    ///
    /// 19.07.2013 Erik Nagel: erstellt
    /// 15.12.2020 Erik Nagel: Warte-Durchlauf bei Übergabe von "UserRun" als letztem Parameter implementiert.
    /// </remarks>
    public class TimerTrigger : INodeTrigger
    {
        #region public members

        #region INodeTrigger Implementation

        /// <summary>
        /// Enthält Informationen zum besitzenden Trigger.
        /// Implementiert sind NextRun und NextRunInfo. Für das Hinzufügen weiterer
        /// Informationen kann diese Klasse abgeleitet werden.
        /// </summary>
        public TriggerInfo Info
        {
            get
            {
                string info = null;
                if (this._triggerActive)
                {
                    info = this._nextTimerStart.ToString();
                    this._info.NextRun = this._nextTimerStart;
                }
                else
                {
                    this._info.NextRun = DateTime.MinValue;
                }
                this._info.NextRunInfo = info;
                return this._info;
            }
            set
            {
            }
        }

        /// <summary>
        /// Startet den Trigger; vorher sollte sich der Consumer in TriggerIt eingehängt haben.
        /// </summary>
        /// <param name="triggerController">Das Objekt, das den Trigger aufruft.</param>
        /// <param name="triggerParameters">Zeit bis zum ersten Start und Intervall durch Pipe ('|') getrennt.
        /// Die Zeitangaben bestehen aus Einheit und Wert durch Doppelpunkt getrennt.
        /// Einheiten sind: "MS" Millisekunden, "S" Sekunden, "M" Minuten, "H" Stunden und "D" Tage.</param>
        /// <param name="triggerIt">Die aufzurufende Callback-Routine, wenn der Trigger feuert.</param>
        /// <returns>True, wenn der Trigger durch diesen Aufruf tatsächlich gestartet wurde.</returns>
        public bool Start(object triggerController, object triggerParameters, Action<TreeEvent> triggerIt)
        {
            // TimeSpan firstRun, TimeSpan interval
            bool isUserRun = triggerParameters.ToString().EndsWith("|UserRun");

            string firstArg = (triggerParameters.ToString() + "|").Split('|')[0];
            string secondArg = "";
            if (!firstArg.Equals(""))
            {
                secondArg = (triggerParameters.ToString() + "|").Split('|')[1];
                if (secondArg.Equals("") || secondArg.Equals("UserRun"))
                {
                    secondArg = firstArg;
                    firstArg = "S:0"; // 0 Sekunden bis zum ersten Trigger-Event
                }
            }
            else
            {
                this.syntax("Es muss zumindest eine Zeitangabe erfolgen.");
            }
            if (isUserRun)
            {
                firstArg = secondArg; // erstmal einen Duschlauf warten, da der Knoten durch UserRun schon direkt gestartet wurde.
            }
            string firstUnity = (firstArg + ":").Split(':')[0].ToUpper();
            if (!(new string[] { "MS", "S", "M", "H", "D" }).Contains(firstUnity))
            {
                this.syntax("Es wurde keine gültige Einheit (MS, S, M, H, D) angegeben.");
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
                this.syntax("Es wurde keine gültige Einheit (MS, S, M, H, D) angegeben.");
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
            if (this.NodeCancellationTokenSource != null)
            {
                this.NodeCancellationTokenSource.Dispose();
            }
            this.triggerIt += triggerIt;
            this.NodeCancellationTokenSource = new CancellationTokenSource();
            this._cancellationToken = this.NodeCancellationTokenSource.Token;
            this._cancellationToken.Register(() => cancelNotification());
            this._timerTask = Observable.Timer(this._firstRun, this._interval);
            this._lastTimerStart = DateTime.MinValue;
            this._nextTimerStart = DateTime.Now.AddMilliseconds(this._firstRun.TotalMilliseconds);
            this._cancellationToken.Register(this._timerTask.Subscribe(this.onTriggerFired).Dispose);
            this._triggerActive = true;
            return true;
        }

        private void syntax(string errorMessage)
        {
            string exeName = Assembly.GetExecutingAssembly().GetName().Name;
            throw new ArgumentException(exeName
              + ": " + errorMessage
              + Environment.NewLine
              + "Aufruf: " + exeName + " [Verzögerung] Interval"
              + Environment.NewLine
              + "Format von Verzögerung, Interval: Einheit + ':' + ganzzahliger Wert"
              + Environment.NewLine
              + "Einheit: MS=Millisekunden, S=Sekunden, M=Minuten, H=Stunden, D=Tage."
              + Environment.NewLine
              + "Beispiel: TimerTrigger S:5 M:10 (feuert nach einer Wartezeit von 5 Sekunden alle 10 Minuten)"
             );
        }

        /// <summary>
        /// Stoppt den Trigger.
        /// </summary>
        /// <param name="triggerController">Das Objekt, das den Trigger aufruft.</param>
        /// <param name="triggerIt">Die aufzurufende Callback-Routine, wenn der Trigger feuert.</param>
        public void Stop(object triggerController, Action<TreeEvent> triggerIt)
        {
            if (this.NodeCancellationTokenSource != null)
            {
                this.NodeCancellationTokenSource.Cancel();
                this._triggerActive = false;
                this._lastTimerStart = DateTime.Now;
                this._nextTimerStart = this._lastTimerStart.AddMilliseconds(this._interval.TotalMilliseconds);
                this.triggerIt -= triggerIt;
            }
        }

        #endregion INodeTrigger Implementation

        /// <summary>
        /// Standard Konstruktor.
        /// </summary>
        public TimerTrigger()
        {
            this._info = new TriggerInfo() { NextRun = DateTime.MinValue, NextRunInfo = null };
            this._lastTimerStart = DateTime.MinValue;
            this._nextTimerStart = DateTime.MinValue;
            this._triggerActive = false;
        }

        #endregion public members

        #region protected members

        /// <summary>
        /// Trigger-Event auslösen.
        /// </summary>
        /// <param name="dummy">Aus Kompatibilitätsgründen, wird hier nicht genutzt.</param>
        protected void onTriggerFired(long dummy)
        {
            this._lastTimerStart = DateTime.Now;
            this._nextTimerStart = this._lastTimerStart.AddMilliseconds(this._interval.TotalMilliseconds);
            if (this.triggerIt != null)
            {
                this.triggerIt(new TreeEvent("TimerTrigger", "TimerTrigger", this.Info.NextRunInfo,
                                  "", "", null, 0, null, null));
            }
        }

        #endregion protected members

        #region private members

        private IObservable<long> _timerTask;
        private CancellationTokenSource NodeCancellationTokenSource { get; set; }
        private CancellationToken _cancellationToken;
        private TriggerInfo _info;
        private DateTime _lastTimerStart;
        private DateTime _nextTimerStart;
        private bool _triggerActive;

        /// <summary>
        /// Wird ausgelöst, wenn das Trigger-Ereignis (z.B. Timer) eintritt. 
        /// </summary>
        private event Action<TreeEvent> triggerIt;

        /// <summary>
        /// Definierter Zeitpunkt für den ersten Start
        /// des zugeorneten INodeWorker oder null.
        /// </summary>
        private TimeSpan _firstRun { get; set; }

        /// <summary>
        /// Intervall für den wiederholten Start (Run)
        /// der zugeorneten INodeWorkr oder null.
        /// </summary>
        private TimeSpan _interval { get; set; }

        /// <summary>
        /// Informiert über den Abbruch der Verarbeitung.
        /// </summary>
        private void cancelNotification()
        {
            // noppes
        }

        #endregion private members
    }
}
