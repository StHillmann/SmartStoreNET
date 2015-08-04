﻿using System;
using System.Collections.Generic;
using System.Linq;
using SmartStore.Core.Data;
using SmartStore.Core.Domain;
using SmartStore.Core.Domain.Tasks;
using SmartStore.Core.Events;
using SmartStore.Core.Plugins;
using SmartStore.Services.Tasks;
using SmartStore.Utilities;

namespace SmartStore.Services.DataExchange
{
	public partial class ExportService : IExportService
	{
		private const int _defaultSchedulingHours = 6;

		private readonly IRepository<ExportProfile> _exportProfileRepository;
		private readonly IEventPublisher _eventPublisher;
		private readonly IScheduleTaskService _scheduleTaskService;
		private readonly IProviderManager _providerManager;

		public ExportService(
			IRepository<ExportProfile> exportProfileRepository,
			IEventPublisher eventPublisher,
			IScheduleTaskService scheduleTaskService,
			IProviderManager providerManager)
		{
			_exportProfileRepository = exportProfileRepository;
			_eventPublisher = eventPublisher;
			_scheduleTaskService = scheduleTaskService;
			_providerManager = providerManager;
		}

		#region Export profiles

		public virtual ExportProfile InsertExportProfile(Provider<IExportProvider> provider)
		{
			if (provider == null)
				throw new ArgumentNullException("provider");

			var name = provider.Metadata.FriendlyName;
			var systemName = provider.Metadata.SystemName;

			if (name.IsEmpty())
				name = systemName;

			var folderName = SeoHelper.GetSeName(name, true, false).ToValidPath();
			var taskType = "SmartStore.Services.DataExchange.{0}Task.{1}, SmartStore.Services".FormatInvariant(systemName, CommonHelper.GenerateRandomDigitCode(10));

			var task = new ScheduleTask
			{
				Name = string.Concat(name, " Export Task"),
				Seconds = _defaultSchedulingHours * 3600,
				Type = taskType,
				Enabled = false,
				StopOnError = false,
				IsHidden = true
			};

			_scheduleTaskService.InsertTask(task);

			var profile = new ExportProfile
			{
				Name = name,
				FolderName = folderName,
				Enabled = true,
				ProviderSystemName = systemName,
				SchedulingTaskId = task.Id
			};			

			_exportProfileRepository.Insert(profile);

			task.Alias = profile.Id.ToString();
			_scheduleTaskService.UpdateTask(task);

			_eventPublisher.EntityInserted(profile);

			return profile;
		}

		public virtual void UpdateExportProfile(ExportProfile profile)
		{
			if (profile == null)
				throw new ArgumentNullException("profile");

			_exportProfileRepository.Update(profile);

			_eventPublisher.EntityUpdated(profile);
		}

		public virtual void DeleteExportProfile(ExportProfile profile)
		{
			if (profile == null)
				throw new ArgumentNullException("profile");

			int scheduleTaskId = profile.SchedulingTaskId;

			_exportProfileRepository.Delete(profile);

			var scheduleTask = _scheduleTaskService.GetTaskById(scheduleTaskId);
			_scheduleTaskService.DeleteTask(scheduleTask);

			_eventPublisher.EntityDeleted(profile);
		}

		public virtual IQueryable<ExportProfile> GetExportProfiles(bool? enabled = null)
		{
			var query =
				from x in _exportProfileRepository.Table.Expand(x => x.ScheduleTask)
				select x;

			if (enabled.HasValue)
			{
				query = query.Where(x => x.Enabled == enabled.Value);
			}

			return query.OrderBy(x => x.Name);
		}

		public virtual ExportProfile GetExportProfileById(int id)
		{
			if (id == 0)
				return null;

			return _exportProfileRepository.GetById(id);
		}


		public virtual IEnumerable<Provider<IExportProvider>> LoadAllExportProviders(int storeId = 0)
		{
			var allProviders = _providerManager.GetAllProviders<IExportProvider>(storeId)
				.Where(x => x.IsValid());

			return allProviders;
		}

		public virtual Provider<IExportProvider> LoadProvider(string systemName, int storeId = 0)
		{
			var provider = _providerManager.GetProvider<IExportProvider>(systemName, storeId);

			return (provider.IsValid() ? provider : null);
		}

		#endregion
	}
}