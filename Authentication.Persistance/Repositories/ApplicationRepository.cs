﻿using AuthenticationServer.Common.Enums;
using AuthenticationServer.Common.Exceptions;
using AuthenticationServer.Common.Extentions;
using AuthenticationServer.Common.Interfaces.Domain.DataAccess;
using AuthenticationServer.Common.Interfaces.Domain.Repositories;
using AuthenticationServer.Domain.Entities;
using Dapper;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Authentication.Persistance.Repositories
{
    public class ApplicationRepository : IApplicationRepository
    {
        private IMainSqlDataAccess _db;

        public ApplicationRepository(IMainSqlDataAccess db)
        {
            _db = db;
        }

        public async Task<ApplicationEntity> GetApplicationFromName(string name)
        {
            string sql = $"SELECT * FROM dbo.Applications where name LIKE @Name";
            var parameters = new { Name = $"{name}%" };

            return await _db.GetData<ApplicationEntity, dynamic>(sql, parameters);
        }

        public async Task<ApplicationEntity> GetApplicationFromHostname(string url)
        {
            string sql = $"SELECT * FROM dbo.Applications where url LIKE @Url";
            var parameters = new { Url = $"{url}%" };

            return await _db.GetData<ApplicationEntity, dynamic>(sql, parameters);
        }

        public async Task<List<ApplicationEntity>> GetApplicationsFromAdminId(Guid adminId)
        {
            string sql = $"SELECT * FROM dbo.Applications where AdminId = @AdminId";
            var parameters = new { AdminId = adminId };

            return await _db.GetData<List<ApplicationEntity>, dynamic>(sql, parameters);
        }

        public async Task<AccountRole?> GetAccountRole(string email)
        {
            string sql = $@"SELECT {nameof(ApplicationUserEntity.AuthenticationRole)} 
                            FROM ApplicationUsers where {nameof(ApplicationUserEntity.Email)} = @Email";

            var parameters = new { Email = email };

            var result = await _db.GetData<string, dynamic>(sql, parameters);

            if (result is null)
                return null;

            return Enum.Parse<AccountRole>(result);
        }

        public async Task<AccountRole?> GetAccountRole(Guid id)
        {
            string sql = $@"SELECT {nameof(ApplicationUserEntity.AuthenticationRole)} 
                            FROM dbo.{typeof(ApplicationEntity).GetTableName()} 
                            where {nameof(ApplicationUserEntity.Id)} = @Id";

            var parameters = new { Id = id.ToString() };

            var result = await _db.GetData<string, dynamic>(sql, parameters);

            if (result is null)
                return null;

            return Enum.Parse<AccountRole>(result);
        }

        public async Task<Guid> Insert(ApplicationEntity applicationEntity, string data = "")
        {
            string sql = $@"INSERT INTO dbo.{typeof(ApplicationEntity).GetTableName()}
                            VALUES (@ApplicationsId, @Name, @AdminId, @MultimediaUUID, @Created, @LastModified);";

            var parameters = new DynamicParameters();
            parameters.AddColumnParameters(applicationEntity);

            await _db.SaveData(sql, parameters);

            return applicationEntity.Id;
        }

        public async Task<ApplicationEntity> Get(Guid? adminId, Guid id)
        {
            string sql = $@"SELECT app.*, jwt.*, dn.* 
                            FROM dbo.{typeof(ApplicationEntity).GetTableName()} app
                            LEFT JOIN dbo.{typeof(JwtTenantConfigEntity).GetTableName()} jwt
                                ON app.{nameof(ApplicationEntity.Id)} = jwt.{nameof(JwtTenantConfigEntity.ApplicationId)}
                            LEFT JOIN dbo.{typeof(DomainNameEntity).GetTableName()} dn
                                ON app.{nameof(ApplicationEntity.Id)} = dn.{nameof(DomainNameEntity.ApplicationId)}
                            WHERE app.{nameof(ApplicationEntity.Id)} = @Id;";

            var parameters = new { Id = id };

            var application = await _db.GetData<ApplicationEntity,
                JwtTenantConfigEntity, DomainNameEntity, ApplicationEntity, dynamic>(sql, parameters,
                    (application, jwtConfigs, domains) =>
                    {
                        application.JwtTenantConfigurations.Add(jwtConfigs);
                        application.Domains.Add(domains);

                        return application;
                    });

            if (application.AdminId != adminId)
                throw new AuthenticationApiException("Application", "UNAUTHORIZED", 403);

            return application;
        }

        public async Task<List<ApplicationEntity>> GetAll(Guid adminId)
        {
            string sql = $@"SELECT app.*, jwt.*, dn.* 
                            FROM dbo.{typeof(ApplicationEntity).GetTableName()} app
                            LEFT JOIN dbo.{typeof(JwtTenantConfigEntity).GetTableName()} jwt
                                ON app.{nameof(ApplicationEntity.Id)} = jwt.{nameof(JwtTenantConfigEntity.ApplicationId)}
                            LEFT JOIN dbo.{typeof(DomainNameEntity).GetTableName()} dn
                                ON app.{nameof(ApplicationEntity.Id)} = dn.{nameof(DomainNameEntity.ApplicationId)}
                            WHERE {nameof(ApplicationUserEntity.AdminId)} = @AdminId";

            var parameters = new { AdminId = adminId };

            var applications = await _db.GetAllData<ApplicationEntity,
                JwtTenantConfigEntity, DomainNameEntity, ApplicationEntity, dynamic>(sql, parameters,
                    (application, jwtConfigs, domains) =>
                    {
                        application.JwtTenantConfigurations.Add(jwtConfigs);
                        application.Domains.Add(domains);

                        return application;
                    });

            return applications;
        }

        public async Task Update(Guid adminId, Guid id, ApplicationEntity applicationEntity)
        {
            await Get(adminId, id);

            // TODO AUTO set LastModified
            string sql = $@"UPDATE dbo.{typeof(ApplicationEntity).GetTableName()}
                            SET 
                                {nameof(ApplicationEntity.Name)} = @Name,
                                {nameof(ApplicationEntity.MultimediaUUID)} = @MultimediaUUID,
                                {nameof(ApplicationEntity.LastModified)} = @LastModified
                            WHERE {nameof(ApplicationEntity.Id)} = @ApplicationsId;";

            var parameters = new DynamicParameters();
            parameters.AddColumnParameters(applicationEntity, nameof(ApplicationEntity.AdminId), nameof(ApplicationEntity.Created));

            await _db.SaveData(sql, parameters);
        }

        public async Task Delete(Guid adminId, Guid id)
        {
            await Get(adminId, id);

            string sql = $@"DELETE dbo.{typeof(ApplicationEntity).GetTableName()}
                            WHERE {nameof(ApplicationUserEntity.Id)} = @Id";

            var parameters = new { Id = id };

            await _db.SaveData(sql, parameters);
        }

        public async Task<Guid> GetApplicationIconUUID(Guid applicationId)
        {
            string sql = $@"SELECT {nameof(ApplicationEntity.MultimediaUUID)} 
                            FROM dbo.{typeof(ApplicationEntity).GetTableName()} 
                            WHERE {nameof(ApplicationUserEntity.Id)} = @Id";

            var parameters = new { Id = applicationId };

            var iconUUID = await _db.GetData<string, dynamic>(sql, parameters);

            return Guid.Parse(iconUUID);
        }
    }
}
