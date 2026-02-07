import { useEffect, useState, useRef, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { bookService } from '../services/bookService';
import { Book } from '../types/book';
import { useVirtualizer } from '@tanstack/react-virtual';

const BookListPage = () => {
    const [books, setBooks] = useState<Book[]>([]);
    const [searchTerm, setSearchTerm] = useState('');
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);
    const parentRef = useRef<HTMLDivElement>(null);
    const debounceTimer = useRef<NodeJS.Timeout | null>(null);

    const fetchBooks = useCallback(async (search?: string) => {
        try {
            setLoading(true);
            const booksData = await bookService.getBooks(search);
            setBooks(booksData);
            setError(null);
        } catch {
            setError('Failed to fetch books from the library.');
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        fetchBooks();
    }, [fetchBooks]);

    const handleSearchChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        const val = e.target.value;
        setSearchTerm(val);

        if (debounceTimer.current) clearTimeout(debounceTimer.current);
        debounceTimer.current = setTimeout(() => {
            fetchBooks(val);
        }, 500);
    };

    const handleDelete = async (id: number, title: string) => {
        if (window.confirm(`Are you sure you want to delete "${title}"? All associated sales data will be lost.`)) {
            try {
                await bookService.deleteBook(id);
                setBooks(prev => prev.filter(b => b.id !== id));
            } catch {
                setError('Failed to delete book. It may have active sales records.');
            }
        }
    };

    const rowVirtualizer = useVirtualizer({
        count: books.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 80,
        overscan: 5,
    });

    if (loading && books.length === 0) return (
        <div style={pageStyles.centered}>
            <div className="shimmer" style={{ height: '100px', borderRadius: '16px', marginBottom: '2rem' }}></div>
            <div className="shimmer" style={{ height: '600px', borderRadius: '16px' }}></div>
        </div>
    );

    return (
        <div style={pageStyles.container}>
            <div style={pageStyles.header}>
                <div>
                    <h1>IP Portfolio</h1>
                    <p style={pageStyles.subtitle}>High-level management of your intellectual property assets.</p>
                </div>
                <Link to="/books/add" className="btn-primary animate-pulse" style={pageStyles.addBtn}>
                    <span style={{ fontSize: '1.25rem' }}>+</span> Register New Title
                </Link>
            </div>

            <div className="card" style={pageStyles.searchCard}>
                <div style={pageStyles.searchWrapper}>
                    <span style={pageStyles.searchIcon}>🔍</span>
                    <input
                        type="text"
                        placeholder="Search assets by title or author identity..."
                        value={searchTerm}
                        onChange={handleSearchChange}
                        style={pageStyles.searchInput}
                    />
                </div>
            </div>

            {error && <div style={pageStyles.errorBanner}>{error}</div>}

            <div className="card" style={pageStyles.tableCard}>
                <div style={pageStyles.tableHeader}>
                    <div style={{ flex: 2.5 }}>Asset Title & Attribution</div>
                    <div style={{ flex: 1.5 }}>Publication Date</div>
                    <div style={{ flex: 1 }}>Genre</div>
                    <div style={{ flex: 1, textAlign: 'right' }}>Management</div>
                </div>

                <div ref={parentRef} style={pageStyles.scrollArea}>
                    {books.length === 0 && !loading ? (
                        <div style={pageStyles.emptyState}>
                            <div style={pageStyles.emptyIcon}>💎</div>
                            <h3>Portfolio is Empty</h3>
                            <p>Begin building your legacy by registering your first title.</p>
                        </div>
                    ) : (
                        <div style={{ height: `${rowVirtualizer.getTotalSize()}px`, width: '100%', position: 'relative' }}>
                            {rowVirtualizer.getVirtualItems().map(virtualItem => {
                                const book = books[virtualItem.index];
                                if (!book) return null;
                                return (
                                    <div
                                        key={virtualItem.key}
                                        className="row-hover"
                                        style={{
                                            ...pageStyles.row,
                                            height: `${virtualItem.size}px`,
                                            transform: `translateY(${virtualItem.start}px)`
                                        }}
                                    >
                                        <div style={{ flex: 2.5 }}>
                                            <Link to={`/books/edit/${book.id}`} style={pageStyles.bookTitle}>
                                                {book.title}
                                            </Link>
                                            <div style={pageStyles.bookAuthor}>{book.author}</div>
                                        </div>
                                        <div style={{ flex: 1.5, color: 'var(--text-muted)', fontSize: '0.9rem', fontWeight: '600' }}>
                                            {book.publicationDate ? new Date(book.publicationDate).toLocaleDateString(undefined, {
                                                year: 'numeric',
                                                month: 'long',
                                                day: 'numeric'
                                            }) : 'Unscheduled'}
                                        </div>
                                        <div style={{ flex: 1 }}>
                                            <span style={pageStyles.genreBadge}>{book.genre || 'General IP'}</span>
                                        </div>
                                        <div style={{ flex: 1, textAlign: 'right', display: 'flex', gap: '1rem', justifyContent: 'flex-end' }}>
                                            <Link to={`/books/performance/${book.id}`} style={pageStyles.actionIcon} title="Economics">
                                                📊
                                            </Link>
                                            <button
                                                onClick={() => handleDelete(book.id, book.title)}
                                                style={pageStyles.deleteBtn}
                                                title="De-register"
                                            >
                                                🗑️
                                            </button>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </div>
            </div>

            <style>{`
                .row-hover {
                    transition: all 0.2s ease-out;
                }
                .row-hover:hover {
                    background: rgba(255, 255, 255, 0.03) !important;
                    box-shadow: inset 4px 0 0 var(--primary);
                }
            `}</style>
        </div>
    );
};

const pageStyles = {
    container: { animation: 'fadeIn 0.5s ease' },
    header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '3rem' },
    subtitle: { color: 'var(--text-muted)', fontSize: '1.1rem' },
    addBtn: { padding: '0.8rem 1.75rem' },
    searchCard: { padding: '0.75rem', marginBottom: '2rem' },
    searchWrapper: { position: 'relative' as const, flex: 1, display: 'flex', alignItems: 'center' },
    searchIcon: { position: 'absolute' as const, left: '1.25rem', color: '#94a3b8', fontSize: '1.1rem' },
    searchInput: {
        width: '100%',
        padding: '0.85rem 1rem 0.85rem 3.25rem',
        background: 'transparent',
        border: 'none',
        fontSize: '1.1rem',
    },
    tableCard: { padding: 0, overflow: 'hidden' },
    tableHeader: {
        display: 'flex',
        padding: '1.25rem 2rem',
        backgroundColor: 'rgba(255,255,255,0.02)',
        borderBottom: '1px solid var(--border)',
        fontWeight: '800',
        color: '#64748b',
        textTransform: 'uppercase' as const,
        fontSize: '0.7rem',
        letterSpacing: '0.15em',
    },
    scrollArea: { height: '600px', overflow: 'auto', position: 'relative' as const },
    row: {
        position: 'absolute' as const,
        top: 0,
        left: 0,
        width: '100%',
        display: 'flex',
        alignItems: 'center',
        padding: '0 2rem',
        borderBottom: '1px solid var(--border)',
        boxSizing: 'border-box' as const,
    },
    bookTitle: { color: '#fff', textDecoration: 'none', fontWeight: '800', fontSize: '1.1rem', marginBottom: '4px', display: 'block' },
    bookAuthor: { fontSize: '0.85rem', color: 'var(--text-muted)', fontWeight: '600' },
    genreBadge: {
        fontSize: '0.7rem',
        padding: '4px 12px',
        borderRadius: '20px',
        backgroundColor: 'rgba(255,255,255,0.05)',
        color: 'var(--text-muted)',
        fontWeight: '800',
        textTransform: 'uppercase' as const,
        letterSpacing: '0.05em',
        border: '1px solid var(--border)'
    },
    actionIcon: { textDecoration: 'none', fontSize: '1.4rem', cursor: 'pointer', transition: 'transform 0.2s' },
    deleteBtn: { backgroundColor: 'transparent', border: 'none', fontSize: '1.4rem', cursor: 'pointer', color: 'rgba(239, 68, 68, 0.4)' },
    emptyState: { textAlign: 'center' as const, padding: '8rem 2rem' },
    emptyIcon: { fontSize: '4rem', marginBottom: '1.5rem', filter: 'drop-shadow(0 0 20px var(--primary-glow))' },
    errorBanner: { padding: '1.5rem', backgroundColor: 'rgba(239, 68, 68, 0.1)', color: 'var(--danger)', borderRadius: '16px', border: '1px solid var(--danger)', marginBottom: '2rem' },
    centered: { padding: '10rem', textAlign: 'center' as const }
};

export default BookListPage;