using LapTrinhWindows.Models;
using LapTrinhWindows.Models.dto;
using LapTrinhWindows.Repositories.BatchRepository;
using LapTrinhWindows.Repositories.PointRedemptionRepository;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LapTrinhWindows.Services
{
    public interface IPointRedemptionService
    {
        Task<PointRedemptionDTO> CreateAsync(PointRedemptionDTO dto);
        Task<PointRedemptionDTO> UpdateAsync(int id, PointRedemptionDTO dto);
        Task DeleteAsync(int id);
        Task<PointRedemptionDTO?> GetByIdAsync(int id);
        Task<List<PointRedemptionDTO>> GetAllAsync(bool includeInactive = false);
    }

    public class PointRedemptionService : IPointRedemptionService
    {
        private readonly IPointRedemptionRepository _pointRedemptionRepository;
        private readonly IBatchRepository _batchRepository;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PointRedemptionService> _logger;

        public PointRedemptionService(
            IPointRedemptionRepository pointRedemptionRepository,
            ApplicationDbContext context,
            ILogger<PointRedemptionService> logger,
            IBatchRepository batchRepository)
        {
            _pointRedemptionRepository = pointRedemptionRepository;
            _context = context;
            _logger = logger;
            _batchRepository = batchRepository;
        }

        public async Task<PointRedemptionDTO> CreateAsync(PointRedemptionDTO dto)
        {
            // Validate input
            if (dto.StartDate >= dto.EndDate)
            {
                throw new ArgumentException("StartDate must be before EndDate.", nameof(dto));
            }
            if (await _pointRedemptionRepository.ExistsBySKUAsync(dto.SKU))
            {
                throw new InvalidOperationException($"A redemption with SKU {dto.SKU} already exists.");
            }
            if (!await _context.Variants.AnyAsync(v => v.SKU == dto.SKU))
            {
                throw new ArgumentException($"Variant with SKU {dto.SKU} does not exist.", nameof(dto.SKU));
            }
            if (dto.BatchID <= 0)
            {
                throw new ArgumentException("BatchID must be a positive integer.", nameof(dto.BatchID));
            }

            // Start transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get batch and validate quantity
                var batch = await _batchRepository.GetBatchByIdAsync(dto.BatchID);
                if (batch == null)
                {
                    throw new ArgumentException($"Batch with ID {dto.BatchID} does not exist.");
                }
                if (batch.AvailableQuantity < dto.AvailableQuantity)
                {
                    throw new InvalidOperationException($"Batch {batch.BatchID} does not have enough quantity ({batch.AvailableQuantity}) for requested redemption ({dto.AvailableQuantity}).");
                }

                // Update batch quantity
                batch.AvailableQuantity -= dto.AvailableQuantity;
                _context.Batches.Update(batch);

                // Create point redemption
                var pointRedemption = new PointRedemption
                {
                    SKU = dto.SKU,
                    RedemptionName = dto.RedemptionName,
                    PointsRequired = dto.PointsRequired,
                    AvailableQuantity = dto.AvailableQuantity,
                    StartDate = dto.StartDate,
                    EndDate = dto.EndDate,
                    Status = dto.Status,
                    BatchID = dto.BatchID
                };

                var created = await _pointRedemptionRepository.CreateAsync(pointRedemption);
                await transaction.CommitAsync();

                return new PointRedemptionDTO
                {
                    PointRedemptionID = created.PointRedemptionID,
                    SKU = created.SKU,
                    RedemptionName = created.RedemptionName,
                    PointsRequired = created.PointsRequired,
                    AvailableQuantity = created.AvailableQuantity,
                    StartDate = created.StartDate,
                    EndDate = created.EndDate,
                    Status = created.Status,
                    BatchID = created.BatchID
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to create PointRedemption for SKU {SKU}", dto.SKU);
                throw;
            }
        }

        public async Task<PointRedemptionDTO> UpdateAsync(int id, PointRedemptionDTO dto)
        {
           
            if (dto.StartDate >= dto.EndDate)
            {
                throw new ArgumentException("StartDate must be before EndDate.", nameof(dto));
            }
            if (!await _context.Variants.AnyAsync(v => v.SKU == dto.SKU))
            {
                throw new ArgumentException($"Variant with SKU {dto.SKU} does not exist.", nameof(dto.SKU));
            }
            if (dto.BatchID <= 0)
            {
                throw new ArgumentException("BatchID must be a positive integer.", nameof(dto.BatchID));
            }

            var existing = await _pointRedemptionRepository.GetByIdAsync(id);
            if (existing == null)
            {
                throw new KeyNotFoundException($"PointRedemption with ID {id} not found.");
            }

            // Start transaction
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // Get original and new batch
                var originalBatch = await _batchRepository.GetBatchByIdAsync(existing.BatchID);
                var newBatch = await _batchRepository.GetBatchByIdAsync(dto.BatchID);
                
                if (originalBatch == null || newBatch == null)
                {
                    throw new ArgumentException("Invalid BatchID.");
                }

               
                int quantityDifference = dto.AvailableQuantity - existing.AvailableQuantity;

                
                if (existing.BatchID != dto.BatchID || quantityDifference > 0)
                {
                    if (newBatch.AvailableQuantity < quantityDifference)
                    {
                        throw new InvalidOperationException($"Batch {newBatch.BatchID} does not have enough quantity ({newBatch.AvailableQuantity}) for requested redemption update ({quantityDifference}).");
                    }
                }

                
                if (existing.BatchID == dto.BatchID)
                {
                    originalBatch.AvailableQuantity += existing.AvailableQuantity;
                }
                else
                {
                   
                    originalBatch.AvailableQuantity += existing.AvailableQuantity;
                    _context.Batches.Update(originalBatch);
                }

                
                newBatch.AvailableQuantity -= dto.AvailableQuantity;
                _context.Batches.Update(newBatch);

                
                existing.SKU = dto.SKU;
                existing.RedemptionName = dto.RedemptionName;
                existing.PointsRequired = dto.PointsRequired;
                existing.AvailableQuantity = dto.AvailableQuantity;
                existing.StartDate = dto.StartDate;
                existing.EndDate = dto.EndDate;
                existing.Status = dto.Status;
                existing.BatchID = dto.BatchID;

                var updated = await _pointRedemptionRepository.UpdateAsync(existing);
                await transaction.CommitAsync();

                return new PointRedemptionDTO
                {
                    PointRedemptionID = updated.PointRedemptionID,
                    SKU = updated.SKU,
                    RedemptionName = updated.RedemptionName,
                    PointsRequired = updated.PointsRequired,
                    AvailableQuantity = updated.AvailableQuantity,
                    StartDate = updated.StartDate,
                    EndDate = updated.EndDate,
                    Status = updated.Status,
                    BatchID = updated.BatchID
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to update PointRedemption with ID {ID}", id);
                throw;
            }
        }

        public async Task DeleteAsync(int id)
        {
            var existing = await _pointRedemptionRepository.GetByIdAsync(id);
            if (existing == null)
            {
                throw new KeyNotFoundException($"PointRedemption with ID {id} not found.");
            }

           
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
               
                var batch = await _batchRepository.GetBatchByIdAsync(existing.BatchID);
                if (batch != null)
                {
                    batch.AvailableQuantity += existing.AvailableQuantity;
                    _context.Batches.Update(batch);
                }

                await _pointRedemptionRepository.DeleteAsync(id);
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to delete PointRedemption with ID {ID}", id);
                throw;
            }
        }

        public async Task<PointRedemptionDTO?> GetByIdAsync(int id)
        {
            var pointRedemption = await _pointRedemptionRepository.GetByIdAsync(id);
            if (pointRedemption == null)
            {
                return null;
            }

            return new PointRedemptionDTO
            {
                PointRedemptionID = pointRedemption.PointRedemptionID,
                SKU = pointRedemption.SKU,
                RedemptionName = pointRedemption.RedemptionName,
                PointsRequired = pointRedemption.PointsRequired,
                AvailableQuantity = pointRedemption.AvailableQuantity,
                StartDate = pointRedemption.StartDate,
                EndDate = pointRedemption.EndDate,
                Status = pointRedemption.Status,
                BatchID = pointRedemption.BatchID
            };
        }

        public async Task<List<PointRedemptionDTO>> GetAllAsync(bool includeInactive = false)
        {
            var pointRedemptions = await _pointRedemptionRepository.GetAllAsync(includeInactive);
            return pointRedemptions.Select(pr => new PointRedemptionDTO
            {
                PointRedemptionID = pr.PointRedemptionID,
                SKU = pr.SKU,
                RedemptionName = pr.RedemptionName,
                PointsRequired = pr.PointsRequired,
                AvailableQuantity = pr.AvailableQuantity,
                StartDate = pr.StartDate,
                EndDate = pr.EndDate,
                Status = pr.Status,
                BatchID = pr.BatchID
            }).ToList();
        }
    }
}