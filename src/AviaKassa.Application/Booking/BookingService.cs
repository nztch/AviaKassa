namespace AviaKassa.Application.Booking;

/// <summary>Сервис бронирования мест (FR-06, FR-07, FR-10).</summary>
public sealed class BookingService : IBookingService
{
    private readonly AviaDbContext _db;
    private readonly IFareCalculator _calculator;

    public BookingService(AviaDbContext db, IFareCalculator calculator)
        => (_db, _calculator) = (db, calculator);

    public async Task<Booking> CreateAsync(CreateBookingRequest req, CancellationToken ct)
    {
        var fare = await _db.FareClasses.FirstAsync(c => c.Id == req.FareClassId, ct);

        DateTimeOffset nowUtc = DateTimeOffset.UtcNow;                

        int taken = await _db.Bookings                                 // FIX DEF-01: только активные брони
            .Where(b => b.FareClassId == fare.Id
                     && (b.Status == BookingStatus.Paid
                         || (b.Status == BookingStatus.Held && b.TimeLimitUtc > nowUtc)))
            .SumAsync(b => b.Passengers.Count, ct);

        if (taken + req.Passengers.Count > fare.SeatsTotal)
            throw new SeatsNotAvailableException(fare.Code, fare.SeatsTotal - taken);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            FareClassId = fare.Id,
            Status = BookingStatus.Held,
            CreatedUtc = nowUtc,                                          //FIX DEF-02: время в UTC
            TimeLimitUtc = nowUtc.AddHours(2),
            Passengers = req.Passengers.ToList(),
            TotalAmount = _calculator.Calculate(fare, req.Passengers, null).Amount
        };

        _db.Bookings.Add(booking);
        await _db.SaveChangesAsync(ct);
        return booking;
    }
}
