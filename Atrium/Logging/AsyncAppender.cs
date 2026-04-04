#if WINDOWS
using log4net.Appender;
using log4net.Core;
using log4net.Util;
#endif


namespace Atrium.Logging
{

#if WINDOWS
    public sealed class AsyncAppender : IBulkAppender, IAppender, IOptionHandler, IAppenderAttachable
    {
        // Token: 0x1700008F RID: 143
        // (get) Token: 0x060002FA RID: 762 RVA: 0x0001935C File Offset: 0x0001755C
        // (set) Token: 0x060002FB RID: 763 RVA: 0x00019373 File Offset: 0x00017573
        public string Name { get; set; }

        // Token: 0x060002FC RID: 764 RVA: 0x0001937C File Offset: 0x0001757C
        public void ActivateOptions()
        {
        }

        public AsyncAppender()
        {
            Name ??= nameof(AsyncAppender);
        }

        // Token: 0x060002FF RID: 767 RVA: 0x000193A4 File Offset: 0x000175A4
        public void Close()
        {
            lock (this)
            {
                this._mAppenderAttachedImpl?.RemoveAllAppenders();
            }
        }

        // Token: 0x06000300 RID: 768 RVA: 0x00019400 File Offset: 0x00017600
        public void DoAppend(LoggingEvent loggingEvent)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(this.AsyncAppend), loggingEvent);
        }

        // Token: 0x06000301 RID: 769 RVA: 0x00019424 File Offset: 0x00017624
        public void DoAppend(LoggingEvent[] loggingEvents)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(this.AsyncAppend), loggingEvents);
        }

        // Token: 0x06000302 RID: 770 RVA: 0x00019470 File Offset: 0x00017670
        private void AsyncAppend(object? state)
        {
            if (this._mAppenderAttachedImpl != null)
            {
                if (state is LoggingEvent loggingEvent)
                {
                    this._mAppenderAttachedImpl.AppendLoopOnAppenders(loggingEvent);
                }
                else
                {
                    if (state is LoggingEvent[] array)
                    {
                        this._mAppenderAttachedImpl.AppendLoopOnAppenders(array);
                    }
                }
            }
        }

        // Token: 0x06000303 RID: 771 RVA: 0x000194D0 File Offset: 0x000176D0
        public void AddAppender(IAppender newAppender)
        {
            ArgumentNullException.ThrowIfNull(newAppender);
            lock (this)
            {
                this._mAppenderAttachedImpl ??= new AppenderAttachedImpl();
                this._mAppenderAttachedImpl.AddAppender(newAppender);
            }
        }

        // Token: 0x17000091 RID: 145
        // (get) Token: 0x06000304 RID: 772 RVA: 0x00019554 File Offset: 0x00017754
        public AppenderCollection Appenders
        {
            get
            {
                AppenderCollection result;
                lock (this)
                {
                    if (this._mAppenderAttachedImpl == null)
                    {
                        result = AppenderCollection.EmptyCollection;
                    }
                    else
                    {
                        result = this._mAppenderAttachedImpl.Appenders;
                    }
                }
                return result;
            }
        }

        // Token: 0x06000305 RID: 773 RVA: 0x000195BC File Offset: 0x000177BC
        public IAppender GetAppender(string? name)
        {
            IAppender? result;
            lock (this)
            {
                if (this._mAppenderAttachedImpl == null || name == null)
                {
                    result = null;
                }
                else
                {
                    result = this._mAppenderAttachedImpl.GetAppender(name);
                }
            }
            return result!;
        }

        // Token: 0x06000306 RID: 774 RVA: 0x00019628 File Offset: 0x00017828
        public void RemoveAllAppenders()
        {
            lock (this)
            {
                this._mAppenderAttachedImpl?.RemoveAllAppenders();
                this._mAppenderAttachedImpl = null;
            }
        }

        // Token: 0x06000307 RID: 775 RVA: 0x0001968C File Offset: 0x0001788C
        public IAppender RemoveAppender(IAppender appender)
        {
            lock (this)
            {
                if (appender != null && this._mAppenderAttachedImpl != null)
                {
                    return this._mAppenderAttachedImpl.RemoveAppender(appender)!;
                }
            }
            return null!;
        }

        // Token: 0x06000308 RID: 776 RVA: 0x000196F8 File Offset: 0x000178F8
        public IAppender RemoveAppender(string name)
        {
            lock (this)
            {
                if (name != null && this._mAppenderAttachedImpl != null)
                {
                    return this._mAppenderAttachedImpl.RemoveAppender(name)!;
                }
            }
            return null!;
        }

        // Token: 0x04000170 RID: 368
        private AppenderAttachedImpl? _mAppenderAttachedImpl;

    }

#endif
}
