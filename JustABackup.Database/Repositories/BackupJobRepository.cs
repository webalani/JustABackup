﻿using JustABackup.Database.Entities;
using JustABackup.Database.Helpers;
using JustABackup.Database.Enum;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JustABackup.Database.Repositories
{
    public interface IBackupJobRepository
    {
        Task<BackupJob> Get(int id);

        Task<IEnumerable<BackupJob>> Get();

        Task<int> AddHistory(int id);

        Task<bool> UpdateHistory(int historyId, ExitCode status, string message);

        Task<IEnumerable<BackupJobHistory>> GetHistory(int limit);

        Task<int> AddOrUpdate(int? id, string name, IEnumerable<ProviderInstance> providerInstances);
    }

    public class BackupJobRepository : IBackupJobRepository
    {
        private DefaultContext context;
        private IDatabaseEncryptionHelper databaseEncryptionHelper;

        public BackupJobRepository(DefaultContext context, IDatabaseEncryptionHelper databaseEncryptionHelper)
        {
            this.context = context;
            this.databaseEncryptionHelper = databaseEncryptionHelper;
        }

        public async Task<int> AddHistory(int id)
        {
            BackupJobHistory history = new BackupJobHistory();
            history.Started = DateTime.Now;

            context.JobHistory.Add(history);
            await context.SaveChangesAsync();

            return history.ID;
        }

        public async Task<int> AddOrUpdate(int? id, string name, IEnumerable<ProviderInstance> providerInstances)
        {
            BackupJob job = null;
            if (id != null && id > 0)
            {
                job = await context.Jobs.Include(j => j.Providers).FirstOrDefaultAsync(j => j.ID == id);
                job.HasChangedModel = false;
                job.Providers.Clear();
            }
            else
            {
                job = new BackupJob();
                context.Jobs.Add(job);
            }

            job.Name = name;
            job.Providers.AddRange(providerInstances);

            await databaseEncryptionHelper.Encrypt(job);

            await context.SaveChangesAsync();

            return job.ID;
        }

        public async Task<BackupJob> Get(int id)
        {
            BackupJob job = await context
                .Jobs
                .Include(j => j.Providers)
                .ThenInclude(x => x.Provider)
                .Include(j => j.Providers)
                .ThenInclude(x => x.Values)
                .ThenInclude(x => x.Property)
                .FirstOrDefaultAsync(j => j.ID == id);

            await databaseEncryptionHelper.Decrypt(job);
            return job;
        }

        public async Task<IEnumerable<BackupJob>> Get()
        {
            return await context
                .Jobs
                .OrderBy(j => j.Name)
                .ToListAsync();
        }

        public async Task<IEnumerable<BackupJobHistory>> GetHistory(int limit)
        {
            return await context
                .JobHistory
                .OrderByDescending(jh => jh.Started)
                .Take(limit)
                .ToListAsync();
        }

        public async Task<bool> UpdateHistory(int historyId, ExitCode status, string message)
        {
            BackupJobHistory history = await context.JobHistory.FirstOrDefaultAsync(jh => jh.ID == historyId);
            if (history == null)
                return false;

            history.Completed = DateTime.Now;
            history.Message = message;
            history.Status = status;

            await context.SaveChangesAsync();
            return true;
        }
    }
}