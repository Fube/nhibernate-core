using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Cfg;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace NHibernate
{
	public interface INHibernateLogger
	{
		/// <summary>Writes a log entry.</summary>
		/// <param name="logLevel">Entry will be written on this level.</param>
		/// <param name="state">The entry to be written.</param>
		/// <param name="exception">The exception related to this entry.</param>
		void Log(NHibernateLogLevel logLevel, NHibernateLogValues state, Exception exception);

		/// <summary>
		/// Checks if the given <paramref name="logLevel" /> is enabled.
		/// </summary>
		/// <param name="logLevel">level to be checked.</param>
		/// <returns><c>true</c> if enabled.</returns>
		bool IsEnabled(NHibernateLogLevel logLevel);
	}

	public interface INHibernateOptimizedLogger : INHibernateLogger
	{
		void Log<TState>(
			NHibernateLogLevel logLevel,
			TState state,
			Exception exception,
			Func<TState, Exception, string> formatter);
	}

	internal static class NHibernateLoggerMessage
	{
		internal static Action<ILogger, T1, Exception> Define<T1>(NHibernateLogLevel logLevel, string format) => LoggerMessage.Define<T1>(Translate(logLevel), new EventId(-1), format);

		internal static Action<ILogger, T1, T2, Exception> Define<T1, T2>(NHibernateLogLevel logLevel, string format) => LoggerMessage.Define<T1, T2>(Translate(logLevel), new EventId(-1), format);

		internal static class TranslatingLogger
		{
			internal static ILogger Create(INHibernateLogger logger)
			{
				if (logger is INHibernateOptimizedLogger optimizedLogger)
				{
					return new OptimizedLogger(optimizedLogger);
				}

				return new NormalLogger(logger);
			}

			private sealed class OptimizedLogger : ILogger
			{
				private readonly INHibernateOptimizedLogger _logger;

				public OptimizedLogger(INHibernateOptimizedLogger logger)
				{
					_logger = logger;
				}

				public void Log<TState>(LogLevel logLevel, EventId _, TState state, Exception exception, Func<TState, Exception, string> formatter)
				{
					var level = Translate(logLevel);
				
					if (!_logger.IsEnabled(level))
					{
						return;
					}
					
					_logger.Log(level, state, exception, formatter);
				}

				public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(Translate(logLevel));

				public IDisposable BeginScope<TState>(TState state) where TState : notnull => throw new NotImplementedException();
			}

			private sealed class NormalLogger : ILogger
			{
				private readonly INHibernateLogger _logger;

				public NormalLogger(INHibernateLogger logger)
				{
					_logger = logger;
				}

				public void Log<TState>(LogLevel logLevel, EventId _, TState state, Exception exception, Func<TState, Exception, string> formatter)
				{
					var level = Translate(logLevel);
				
					if (!_logger.IsEnabled(level))
					{
						return;
					}

					var args = state as IReadOnlyCollection<KeyValuePair<string, object>>;
					var originalFormat = args?.FirstOrDefault(x => x.Key == "OriginalFormat").Value as string;
					var realArgs = args?.Where(x => x.Key != "OriginalFormat");
					_logger.Log(level, new NHibernateLogValues(originalFormat, realArgs?.Select(x => x.Value).ToArray()), exception);
				}

				public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(Translate(logLevel));

				public IDisposable BeginScope<TState>(TState state) where TState : notnull => throw new NotImplementedException();
			}
		}

		private static LogLevel Translate(NHibernateLogLevel logLevel)
		{
			return logLevel switch
			{
				NHibernateLogLevel.Trace => LogLevel.Trace,
				NHibernateLogLevel.Debug => LogLevel.Debug,
				NHibernateLogLevel.Info => LogLevel.Information,
				NHibernateLogLevel.Warn => LogLevel.Warning,
				NHibernateLogLevel.Error => LogLevel.Error,
				NHibernateLogLevel.Fatal => LogLevel.Critical,
				NHibernateLogLevel.None => LogLevel.None,
				_ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
			};
		}
		
		private static NHibernateLogLevel Translate(LogLevel logLevel)
		{
			return logLevel switch
			{
				LogLevel.Trace => NHibernateLogLevel.Trace,
				LogLevel.Debug => NHibernateLogLevel.Debug,
				LogLevel.Information => NHibernateLogLevel.Info,
				LogLevel.Warning => NHibernateLogLevel.Warn,
				LogLevel.Error => NHibernateLogLevel.Error,
				LogLevel.Critical => NHibernateLogLevel.Fatal,
				LogLevel.None => NHibernateLogLevel.None,
				_ => throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null)
			};
		}
	}

	/// <summary>
	/// Factory interface for providing a <see cref="INHibernateLogger"/>.
	/// </summary>
	public interface INHibernateLoggerFactory
	{
		/// <summary>
		/// Get a logger for the given log key.
		/// </summary>
		/// <param name="keyName">The log key.</param>
		/// <returns>A NHibernate logger.</returns>
		INHibernateLogger LoggerFor(string keyName);
		/// <summary>
		/// Get a logger using the given type as log key.
		/// </summary>
		/// <param name="type">The type to use as log key.</param>
		/// <returns>A NHibernate logger.</returns>
		INHibernateLogger LoggerFor(System.Type type);
	}

	/// <summary>
	/// Provide methods for getting NHibernate loggers according to supplied <see cref="INHibernateLoggerFactory"/>.
	/// </summary>
	/// <remarks>
	/// By default, it will use a <see cref="Log4NetLoggerFactory"/> if log4net is available, otherwise it will
	/// use a <see cref="NoLoggingNHibernateLoggerFactory"/>.
	/// </remarks>
	public static class NHibernateLogger
	{
		private static INHibernateLoggerFactory _loggerFactory;

#pragma warning disable 618
		private static ILoggerFactory _legacyLoggerFactory;
		internal static ILoggerFactory LegacyLoggerFactory => LogWrapper.LegacyLoggerFactory; 
#pragma warning restore 618

		private static class LogWrapper
		{
			static LogWrapper()
			{
				var userLoggerFactory = _loggerFactory;
				if (userLoggerFactory == null)
				{
					var nhibernateLoggerClass = GetNhibernateLoggerClass();
					var loggerFactory = string.IsNullOrEmpty(nhibernateLoggerClass) ? null : GetLoggerFactory(nhibernateLoggerClass);
					SetLoggersFactory(loggerFactory);
				}
			}

			public static INHibernateLoggerFactory LoggerFactory
			{
				[MethodImpl(MethodImplOptions.NoInlining)]
				get => _loggerFactory;
			}

#pragma warning disable 618
			internal static ILoggerFactory LegacyLoggerFactory
			{
				[MethodImpl(MethodImplOptions.NoInlining)]
				get => _legacyLoggerFactory;
			}
#pragma warning restore 618
		}

		/// <summary>
		/// Specify the logger factory to use for building loggers.
		/// </summary>
		/// <param name="loggerFactory">A logger factory.</param>
		public static void SetLoggersFactory(INHibernateLoggerFactory loggerFactory)
		{
			_loggerFactory = loggerFactory ?? new NoLoggingNHibernateLoggerFactory();

#pragma warning disable 618
			// Also keep global state for obsolete logger
			if (loggerFactory == null)
			{
				_legacyLoggerFactory = new NoLoggingLoggerFactory();
			}
			else
			{
				if (loggerFactory is LoggerProvider.LegacyLoggerFactoryAdaptor legacyAdaptor)
				{
					_legacyLoggerFactory = legacyAdaptor.Factory;
				}
				else
				{
					_legacyLoggerFactory = new LoggerProvider.ReverseLegacyLoggerFactoryAdaptor(loggerFactory);
				}
			}
#pragma warning restore 618
		}

		/// <summary>
		/// Get a logger for the given log key.
		/// </summary>
		/// <param name="keyName">The log key.</param>
		/// <returns>A NHibernate logger.</returns>
		public static INHibernateLogger For(string keyName)
		{
			return LogWrapper.LoggerFactory.LoggerFor(keyName);
		}

		/// <summary>
		/// Get a logger using the given type as log key.
		/// </summary>
		/// <param name="type">The type to use as log key.</param>
		/// <returns>A NHibernate logger.</returns>
		public static INHibernateLogger For(System.Type type)
		{
			return LogWrapper.LoggerFactory.LoggerFor(type);
		}

		private static string GetNhibernateLoggerClass()
		{
			var nhibernateLoggerClass = ConfigurationProvider.Current.GetLoggerFactoryClassName();
			if (nhibernateLoggerClass == null)
			{
				// look for log4net
				if (Log4NetLoggerFactory.Log4NetAssembly != null)
				{
					nhibernateLoggerClass = typeof(Log4NetLoggerFactory).AssemblyQualifiedName;
				}
			}
			return nhibernateLoggerClass;
		}

		private static INHibernateLoggerFactory GetLoggerFactory(string nhibernateLoggerClass)
		{
			INHibernateLoggerFactory loggerFactory;
			var loggerFactoryType = System.Type.GetType(nhibernateLoggerClass);
			try
			{
				var loadedLoggerFactory = Activator.CreateInstance(loggerFactoryType);
#pragma warning disable 618
				if (loadedLoggerFactory is ILoggerFactory oldStyleFactory)
				{
					loggerFactory = new LoggerProvider.LegacyLoggerFactoryAdaptor(oldStyleFactory);
				}
#pragma warning restore 618
				else
				{
					loggerFactory = (INHibernateLoggerFactory) loadedLoggerFactory;
				}
			}
			catch (MissingMethodException ex)
			{
				throw new InstantiationException("Public constructor was not found for " + loggerFactoryType, ex, loggerFactoryType);
			}
			catch (InvalidCastException ex)
			{
#pragma warning disable 618
				throw new InstantiationException(loggerFactoryType + "Type does not implement " + typeof(INHibernateLoggerFactory) + " or " + typeof(ILoggerFactory), ex, loggerFactoryType);
#pragma warning restore 618
			}
			catch (Exception ex)
			{
				throw new InstantiationException("Unable to instantiate: " + loggerFactoryType, ex, loggerFactoryType);
			}
			return loggerFactory;
		}
	}

	internal class NoLoggingNHibernateLoggerFactory: INHibernateLoggerFactory
	{
		private static readonly INHibernateLogger Nologging = new NoLoggingNHibernateLogger();
		public INHibernateLogger LoggerFor(string keyName)
		{
			return Nologging;
		}

		public INHibernateLogger LoggerFor(System.Type type)
		{
			return Nologging;
		}
	}

	internal class NoLoggingNHibernateLogger: INHibernateLogger, INHibernateOptimizedLogger
	{
		public void Log(NHibernateLogLevel logLevel, NHibernateLogValues state, Exception exception)
		{
		}

		public bool IsEnabled(NHibernateLogLevel logLevel)
		{
			if (logLevel == NHibernateLogLevel.None) return true;

			return false;
		}

		public void Log<TState>(NHibernateLogLevel logLevel, TState state, Exception exception, Func<TState, Exception, string> formatter)
		{
		}
	}

	public struct NHibernateLogValues
	{
		private readonly string _format;
		private readonly object[] _args;

		/// <summary>
		/// Instantiates a new instance of the <see cref="NHibernateLogValues"/> structure.
		/// </summary>
		/// <param name="format">A composite format string</param>
		/// <param name="args">An object array that contains zero or more objects to format.  Can be <c>null</c> if there are no values to format.</param>
		public NHibernateLogValues(string format, object[] args)
		{
			_format = format ?? "[Null]";
			_args = args ?? Array.Empty<object>();
		}

		/// <summary>
		/// Returns the composite format string.
		/// </summary>
		/// <remarks>
		/// A composite format string consists of zero or more runs of fixed text intermixed with
		/// one or more format items, which are indicated by an index number delimited with brackets
		/// (for example, {0}). The index of each format item corresponds to an argument in an object
		/// list that follows the composite format string.
		/// </remarks>
		public string Format => _format;

		/// <summary>
		/// An object array that contains zero or more objects to format.  Can be <c>null</c> if there are no values to format.
		/// </summary>
		public object[] Args => _args;

		/// <summary>
		/// Returns the string that results from formatting the composite format string along with
		/// its arguments by using the formatting conventions of the current culture.
		/// </summary>
		public override string ToString()
		{
			return _args?.Length > 0 ? string.Format(_format, _args) : Format;
		}
	}

	/// <summary>Defines logging severity levels.</summary>
	public enum NHibernateLogLevel
	{
		Trace,
		Debug,
		Info,
		Warn,
		Error,
		Fatal,
		None,
	}
}
