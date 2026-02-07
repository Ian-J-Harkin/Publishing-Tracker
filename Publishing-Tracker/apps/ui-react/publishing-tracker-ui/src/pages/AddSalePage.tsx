import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { saleService } from '../services/saleService';
import { bookService } from '../services/bookService';
import { platformService } from '../services/platformService';
import { CreateSale } from '../types/sale';
import { Book } from '../types/book';
import { Platform } from '../types/platform';

const AddSalePage = () => {
    const navigate = useNavigate();
    const [sale, setSale] = useState<CreateSale>({
        bookId: 0,
        platformId: 0,
        saleDate: new Date().toISOString().split('T')[0],
        quantity: 1,
        unitPrice: 0,
        royalty: 0,
        currency: 'USD'
    });
    const [books, setBooks] = useState<Book[]>([]);
    const [platforms, setPlatforms] = useState<Platform[]>([]);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchData = async () => {
            try {
                const [availableBooks, availablePlatforms] = await Promise.all([
                    bookService.getBooks(),
                    platformService.getPlatforms()
                ]);
                setBooks(availableBooks);
                setPlatforms(availablePlatforms);
            } catch {
                setError('Failed to fetch required metadata for transaction logging.');
            }
        };
        fetchData();
    }, []);

    const handleChange = (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
        const { name, value } = e.target;
        const processedValue = (name === 'bookId' || name === 'platformId' || name === 'quantity' || name === 'unitPrice' || name === 'royalty')
            ? Number(value)
            : value;

        setSale(prev => ({ ...prev, [name]: processedValue }));
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        if (sale.bookId === 0 || sale.platformId === 0) {
            setError('Selection Required: Please specify both target publication and distribution channel.');
            return;
        }

        try {
            await saleService.createSale(sale);
            navigate('/sales');
        } catch {
            setError('Transmission Error: Failed to secure transaction data on the ledger.');
        }
    };

    const projectedRevenue = (sale.quantity * sale.royalty).toFixed(2);

    return (
        <div style={pageStyles.wrapper}>
            <div style={pageStyles.header}>
                <h1>Manual Entry</h1>
                <p style={{ color: 'var(--text-muted)', fontSize: '1.1rem' }}>Log direct-to-consumer sales or platform-specific manual entries.</p>
            </div>

            <div className="card" style={pageStyles.formCard}>
                {error && <div style={pageStyles.errorBanner}>{error}</div>}

                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Intellectual Property Asset</label>
                        <select name="bookId" value={sale.bookId} onChange={handleChange} required>
                            <option value="0">--- Select Publication Title ---</option>
                            {books.map(book => (
                                <option key={book.id} value={book.id}>{book.title}</option>
                            ))}
                        </select>
                    </div>

                    <div style={pageStyles.gridRow}>
                        <div className="form-group" style={{ flex: 2 }}>
                            <label>Sales Channel</label>
                            <select name="platformId" value={sale.platformId} onChange={handleChange} required>
                                <option value="0">--- Distribution Platform ---</option>
                                {platforms.map(platform => (
                                    <option key={platform.id} value={platform.id}>{platform.name}</option>
                                ))}
                            </select>
                        </div>
                        <div className="form-group" style={{ flex: 1.2 }}>
                            <label>Transaction Date</label>
                            <input
                                type="date"
                                name="saleDate"
                                value={sale.saleDate}
                                onChange={handleChange}
                                required
                            />
                        </div>
                    </div>

                    <div style={pageStyles.gridRow}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Volume (Units)</label>
                            <input
                                type="number"
                                name="quantity"
                                min="1"
                                value={sale.quantity}
                                onChange={handleChange}
                                required
                            />
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Reporting Currency</label>
                            <select name="currency" value={sale.currency} onChange={handleChange}>
                                <option value="USD">USD ($)</option>
                                <option value="GBP">GBP (£)</option>
                                <option value="EUR">EUR (€)</option>
                                <option value="CAD">CAD ($)</option>
                                <option value="AUD">AUD ($)</option>
                            </select>
                        </div>
                    </div>

                    <div style={pageStyles.gridRow}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Market Price ({sale.currency})</label>
                            <input
                                type="number"
                                step="0.01"
                                name="unitPrice"
                                value={sale.unitPrice}
                                onChange={handleChange}
                                required
                                placeholder="0.00"
                            />
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Accrued Royalty ({sale.currency})</label>
                            <input
                                type="number"
                                step="0.01"
                                name="royalty"
                                value={sale.royalty}
                                onChange={handleChange}
                                required
                                placeholder="0.00"
                            />
                        </div>
                    </div>

                    <div style={pageStyles.revenueProjection}>
                        <div style={pageStyles.projectionMeta}>
                            <span style={pageStyles.projectionLabel}>Net Projected Yield</span>
                            <span style={pageStyles.projectionSub}>Dynamic Calculation (Volume × Royalty)</span>
                        </div>
                        <div style={pageStyles.projectionValue}>
                            {sale.currency} {projectedRevenue}
                        </div>
                    </div>

                    <div style={pageStyles.actions}>
                        <button type="button" onClick={() => navigate('/sales')} style={pageStyles.cancelBtn}>
                            Discard Entry
                        </button>
                        <button type="submit" className="btn-primary" style={{ padding: '0.85rem 2.5rem' }}>
                            Validate & Commit
                        </button>
                    </div>
                </form>
            </div>

            <style>{`
                @keyframes glowPulse {
                    0% { box-shadow: 0 0 5px rgba(56, 189, 248, 0.1); }
                    50% { box-shadow: 0 0 20px rgba(56, 189, 248, 0.2); }
                    100% { box-shadow: 0 0 5px rgba(56, 189, 248, 0.1); }
                }
            `}</style>
        </div>
    );
};

const pageStyles = {
    wrapper: { maxWidth: '750px', margin: '0 auto', animation: 'fadeIn 0.5s ease-out' },
    header: { marginBottom: '3rem' },
    formCard: { padding: '3rem', border: '1px solid rgba(255,255,255,0.1)' },
    gridRow: { display: 'flex', gap: '2rem', marginBottom: '1rem' },
    errorBanner: {
        backgroundColor: 'rgba(239, 68, 68, 0.1)',
        color: 'var(--danger)',
        padding: '1.25rem',
        borderRadius: '12px',
        marginBottom: '2.5rem',
        border: '1px solid var(--danger)',
        textAlign: 'center' as const,
        fontSize: '0.95rem',
        fontWeight: '600'
    },
    revenueProjection: {
        marginTop: '2.5rem',
        padding: '2rem',
        background: 'rgba(56, 189, 248, 0.05)',
        borderRadius: '16px',
        border: '1px solid rgba(56, 189, 248, 0.2)',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        animation: 'glowPulse 3s infinite ease-in-out'
    },
    projectionMeta: { display: 'flex', flexDirection: 'column' as const, gap: '4px' },
    projectionLabel: { fontWeight: '800', color: '#fff', fontSize: '1.2rem', letterSpacing: '-0.01em' },
    projectionSub: { fontSize: '0.8rem', color: 'var(--text-muted)', fontWeight: '600', textTransform: 'uppercase' as const, letterSpacing: '0.05em' },
    projectionValue: {
        fontSize: '2rem',
        fontWeight: '900',
        color: 'var(--primary)',
        textShadow: '0 0 20px var(--primary-glow)'
    },
    actions: {
        display: 'flex',
        justifyContent: 'flex-end',
        gap: '2rem',
        marginTop: '3rem',
        paddingTop: '2rem',
        borderTop: '1px solid var(--border)',
        alignItems: 'center'
    },
    cancelBtn: {
        backgroundColor: 'transparent',
        border: 'none',
        color: 'var(--text-muted)',
        fontWeight: '700',
        cursor: 'pointer',
        fontSize: '0.95rem',
        textTransform: 'uppercase' as const,
        letterSpacing: '0.1em'
    }
};

export default AddSalePage;