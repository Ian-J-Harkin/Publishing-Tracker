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
                setError('Failed to fetch required data for sales entry.');
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
            setError('Please select both a book and a platform.');
            return;
        }

        try {
            await saleService.createSale(sale);
            navigate('/sales');
        } catch {
            setError('Failed to log transaction. Please check your network or entry data.');
        }
    };

    const projectedRevenue = (sale.quantity * sale.royalty).toFixed(2);

    return (
        <div style={pageStyles.wrapper}>
            <div style={pageStyles.header}>
                <h1>Manual Sales Entry</h1>
                <p style={{ color: 'var(--text-muted)' }}>Use this for platforms that don't support CSV exports or for one-off direct sales.</p>
            </div>

            <div className="card" style={pageStyles.formCard}>
                {error && <div style={pageStyles.errorBanner}>{error}</div>}

                <form onSubmit={handleSubmit}>
                    <div className="form-group">
                        <label>Target Publication</label>
                        <select name="bookId" value={sale.bookId} onChange={handleChange} required style={pageStyles.input}>
                            <option value="0">--- Select a Book ---</option>
                            {books.map(book => (
                                <option key={book.id} value={book.id}>{book.title}</option>
                            ))}
                        </select>
                    </div>

                    <div style={pageStyles.gridRow}>
                        <div className="form-group" style={{ flex: 2 }}>
                            <label>Distribution Platform</label>
                            <select name="platformId" value={sale.platformId} onChange={handleChange} required style={pageStyles.input}>
                                <option value="0">--- Select Platform ---</option>
                                {platforms.map(platform => (
                                    <option key={platform.id} value={platform.id}>{platform.name}</option>
                                ))}
                            </select>
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Sale Date</label>
                            <input
                                type="date"
                                name="saleDate"
                                value={sale.saleDate}
                                onChange={handleChange}
                                required
                                style={pageStyles.input}
                            />
                        </div>
                    </div>

                    <div style={pageStyles.gridRow}>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Units Sold</label>
                            <input
                                type="number"
                                name="quantity"
                                min="1"
                                value={sale.quantity}
                                onChange={handleChange}
                                required
                                style={pageStyles.input}
                            />
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Currency</label>
                            <select name="currency" value={sale.currency} onChange={handleChange} style={pageStyles.input}>
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
                            <label>List Price ({sale.currency})</label>
                            <input
                                type="number"
                                step="0.01"
                                name="unitPrice"
                                value={sale.unitPrice}
                                onChange={handleChange}
                                required
                                style={pageStyles.input}
                                placeholder="0.00"
                            />
                        </div>
                        <div className="form-group" style={{ flex: 1 }}>
                            <label>Royalty per Unit ({sale.currency})</label>
                            <input
                                type="number"
                                step="0.01"
                                name="royalty"
                                value={sale.royalty}
                                onChange={handleChange}
                                required
                                style={pageStyles.input}
                                placeholder="0.00"
                            />
                        </div>
                    </div>

                    <div style={pageStyles.revenueProjection}>
                        <div style={pageStyles.projectionMeta}>
                            <span style={pageStyles.projectionLabel}>Calculated Revenue</span>
                            <span style={pageStyles.projectionSub}>Quantity × Royalty</span>
                        </div>
                        <div style={pageStyles.projectionValue}>
                            {sale.currency} {projectedRevenue}
                        </div>
                    </div>

                    <div style={pageStyles.actions}>
                        <button type="button" onClick={() => navigate('/sales')} style={pageStyles.cancelBtn}>
                            Cancel
                        </button>
                        <button type="submit" className="btn-primary" style={{ padding: '0.75rem 2rem' }}>
                            Log Transaction
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
};

const pageStyles = {
    wrapper: { maxWidth: '700px', margin: '0 auto' },
    header: { marginBottom: '2.5rem', textAlign: 'center' as const },
    formCard: { padding: '2.5rem' },
    gridRow: { display: 'flex', gap: '1.5rem', marginBottom: '1rem' },
    input: { width: '100%', padding: '0.75rem', borderRadius: '8px', border: '1px solid var(--border)', fontSize: '1rem' },
    errorBanner: {
        backgroundColor: '#fef2f2',
        color: 'var(--danger)',
        padding: '1rem',
        borderRadius: '12px',
        marginBottom: '2rem',
        border: '1px solid #fee2e2',
        textAlign: 'center' as const,
        fontSize: '0.9rem'
    },
    revenueProjection: {
        marginTop: '2rem',
        padding: '1.5rem',
        backgroundColor: '#f8fafc',
        borderRadius: '12px',
        border: '1px solid var(--border)',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center'
    },
    projectionMeta: { display: 'flex', flexDirection: 'column' as const },
    projectionLabel: { fontWeight: '700', color: 'var(--text-main)', fontSize: '1.1rem' },
    projectionSub: { fontSize: '0.8rem', color: 'var(--text-muted)' },
    projectionValue: { fontSize: '1.5rem', fontWeight: '800', color: 'var(--primary)' },
    actions: {
        display: 'flex',
        justifyContent: 'flex-end',
        gap: '1.5rem',
        marginTop: '2.5rem',
        paddingTop: '1.5rem',
        borderTop: '1px solid var(--border)',
        alignItems: 'center'
    },
    cancelBtn: {
        backgroundColor: 'transparent',
        border: 'none',
        color: 'var(--text-muted)',
        fontWeight: '600',
        cursor: 'pointer',
        fontSize: '1rem'
    }
};

export default AddSalePage;