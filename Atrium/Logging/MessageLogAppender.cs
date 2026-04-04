
using log4net.Appender;
using log4net.Core;
using log4net.Util;


namespace Atrium.Logging
{
    // Token: 0x02000061 RID: 97
    public class MessageLogAppender : IBulkAppender, IAppender, IOptionHandler, IAppenderAttachable
    {
        public string Name { get; set; }

        public string? ConnectionStringName { get; set; }

        static MessageLogAppender()
        {
        }

        public MessageLogAppender()
        {
            Name ??= nameof(MessageLogAppender);
        }


        public void ActivateOptions()
        {
            if (string.IsNullOrEmpty(Name)) Name = nameof(MessageLogAppender);
            //base.ActivateOptions();
        }

        // Token: 0x060002FF RID: 767 RVA: 0x000193A4 File Offset: 0x000175A4
        public void Close()
        {
            lock (this)
            {
                this._mAppenderAttachedImpl?.RemoveAllAppenders();
            }
        }

        public void DoAppend(LoggingEvent[] loggingEvents)
        {
            for (int i = 0; i < loggingEvents.Length; i++)
            {
                DoAppend(loggingEvents[i]);
            }
        }


        public void DoAppend(LoggingEvent loggingEvent)
        {
            _ = DoAppendForget(loggingEvent);
        }

        /*
        protected override void Append(LoggingEvent loggingEvent)
        {
            _ = DoAppendForget(loggingEvent);
        }
        */

        public static async Task DoAppendForget(LoggingEvent loggingEvent)
        {
            try
            {
                if (loggingEvent.ExceptionObject != null)
                {
                    _ = SimpleLogger.DoAppendForget(loggingEvent.LoggerName ?? nameof(MessageLogAppender), loggingEvent.ExceptionObject.Message, loggingEvent.ExceptionObject);
                }
                else
                {
                    _ = SimpleLogger.DoAppendForget(loggingEvent.LoggerName ?? nameof(MessageLogAppender), (loggingEvent.MessageObject?.ToString() ?? loggingEvent.RenderedMessage) ?? string.Empty, loggingEvent.ExceptionObject);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
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

}

