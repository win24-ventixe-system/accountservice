using Microsoft.EntityFrameworkCore;
using Presentation.Data;
using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;

namespace Presentation.Repositories;


public abstract class BaseRepository<TEntity, TModel> : IBaseRepository<TEntity, TModel> where TEntity : class
{
    protected readonly DataContext _context;
    protected readonly DbSet<TEntity> _table;
    protected BaseRepository(DataContext context)
    {
        _context = context;
        _table = _context.Set<TEntity>();
    }
    public virtual async Task<RepositoryResult<bool>> AddAsync(TEntity entity)
    {
        if (entity == null)
            return new RepositoryResult<bool> { Succeeded = false, StatusCode = 400, Error = "Entity cannot be null." };

        try
        {
            _table.Add(entity);
            await _context.SaveChangesAsync();
            return new RepositoryResult<bool> { Succeeded = true, StatusCode = 201 };
        }

        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return new RepositoryResult<bool> { Succeeded = false, StatusCode = 500, Error = ex.Message };
        }
    }



    public virtual async Task<RepositoryResult<IEnumerable<TSelect>>> GetAllAsync<TSelect>(Expression<Func<TEntity, TSelect>> selector, bool orderByDescending = false, Expression<Func<TEntity, object>>? sortBy = null, Expression<Func<TEntity, bool>>? where = null, params Expression<Func<TEntity, object>>[] includes)
    {

        IQueryable<TEntity> query = _table;

        if (where != null)
            query = query.Where(where);
        if (includes != null && includes.Length != 0)
            foreach (var include in includes)
                query.Include(include);
        if (sortBy != null)
            query = orderByDescending
                ? query.OrderByDescending(sortBy)
                : query.OrderBy(sortBy);

        // The 'selector' expression itself performs the mapping within the database query.
        var entities = await query.Select(selector).ToListAsync();

        return new RepositoryResult<IEnumerable<TSelect>> { Succeeded = true, StatusCode = 200, Result = entities };
    }


    public virtual async Task<RepositoryResult<TModel>> GetAsync(Expression<Func<TEntity, bool>> where, params Expression<Func<TEntity, object>>[] includes)
    {

        IQueryable<TEntity> query = _table;


        if (includes != null && includes.Length != 0)
            foreach (var include in includes)
                query = query.Include(include);

        var entity = await query.FirstOrDefaultAsync(where);
        if (entity == null)
            return new RepositoryResult<TModel> { Succeeded = true, StatusCode = 404, Error = "Entity not found." };

        if (entity is TModel modelResult) // Tries to cast directly
        {
            return new RepositoryResult<TModel> { Succeeded = true, StatusCode = 200, Result = modelResult };
        }
        else
        {
            // If neither direct cast nor single-parameter constructor is found
            return new RepositoryResult<TModel> { Succeeded = false, StatusCode = 500, Error = $"No direct mapping or constructor found from {typeof(TEntity).Name} to {typeof(TModel).Name}. Consider providing a mapper." };
        }

    }



    public virtual async Task<RepositoryResult<bool>> ExistsAsync(Expression<Func<TEntity, bool>> findBy)
    {
        var exists = await _table.AnyAsync(findBy);
        return !exists
            ? new RepositoryResult<bool> { Succeeded = false, StatusCode = 404, Error = "Entity not found" }
            : new RepositoryResult<bool> { Succeeded = true, StatusCode = 200 };
    }


    public virtual async Task<RepositoryResult<bool>> UpdateAsync(TEntity entity)
    {
        if (entity == null)
            return new RepositoryResult<bool> { Succeeded = false, StatusCode = 400, Error = "Entity can't be null." };

        try
        {
            // Check if entity exists in database
            var exists = await _table.AnyAsync(e => EF.Property<string>(e, "Id") == EF.Property<string>(entity, "Id"));
            if (!exists)
            {
                return new RepositoryResult<bool>
                {
                    Succeeded = false,
                    StatusCode = 404,
                    Error = "Entity not found in database."
                };
            }

            // Ensure entity is being tracked
            _context.Entry(entity).State = EntityState.Modified;

            // Save changes
            await _context.SaveChangesAsync();
            return new RepositoryResult<bool> { Succeeded = true, StatusCode = 200 };
        }
        catch (Exception ex)
        {

            return new RepositoryResult<bool> { Succeeded = false, StatusCode = 500, Error = ex.Message };
        }
    }


    public virtual async Task<RepositoryResult<bool>> DeleteAsync(TEntity entity)
    {
        if (entity == null)
            return new RepositoryResult<bool> { Succeeded = false, StatusCode = 400, Error = "Entity can't be null" };

        try
        {
            _table.Remove(entity);
            await _context.SaveChangesAsync();
            return new RepositoryResult<bool> { Succeeded = true, StatusCode = 200 };
        }
        catch (Exception ex)
        {

            return new RepositoryResult<bool> { Succeeded = false, StatusCode = 500, Error = ex.Message };
        }
    }


}

