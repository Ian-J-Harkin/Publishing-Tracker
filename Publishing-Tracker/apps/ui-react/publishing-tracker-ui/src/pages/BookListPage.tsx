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
        estimateSize: () => 70,
        overscan: 5,
    });

    if (loading && books.length === 0) return <div style={pageStyles.centered}>Scanning Library...</div>;

    return (
        <div style={pageStyles.container}>
            <div style={pageStyles.header}>
                <div>
                    <h1 style={pageStyles.title}>My Portfolio</h1>
                    <p style={pageStyles.subtitle}>Manage your published works and track their performance.</p>
                </div>
                <Link to="/books/add" className="btn-primary" style={pageStyles.addBtn}>
                    + Add New Book
                </Link>
            </div>

            <div className="card" style={pageStyles.searchCard}>
                <div style={pageStyles.searchWrapper}>
                    <span style={pageStyles.searchIcon}>🔍</span>
                    <input
                        type="text"
                        placeholder="Search by title or author..."
                        value={searchTerm}
                        onChange={handleSearchChange}
                        style={pageStyles.searchInput}
                    />
                </div>
            </div>

            {error && <div style={pageStyles.errorBanner}>{error}</div>}

            <div className="card" style={pageStyles.tableCard}>
                <div style={pageStyles.tableHeader}>
                    <div style={{ flex: 2.5 }}>Title & Author</div>
                    <div style={{ flex: 1.5 }}>Release Date</div>
                    <div style={{ flex: 1 }}>Genre</div>
                    <div style={{ flex: 1, textAlign: 'right' }}>Actions</div>
                </div>

                <div ref={parentRef} style={pageStyles.scrollArea}>
                    {books.length === 0 && !loading ? (
                        <div style={pageStyles.emptyState}>
                            <div style={pageStyles.emptyIcon}>📚</div>
                            <h3>No books found</h3>
                            <p>Try adjusting your search or add a new title to your collection.</p>
                        </div>
                    ) : (
                        <div style={{ height: `${rowVirtualizer.getTotalSize()}px`, width: '100%', position: 'relative' }}>
                            {rowVirtualizer.getVirtualItems().map(virtualItem => {
                                const book = books[virtualItem.index];
                                if (!book) return null;
                                return (
                                    <div
                                        key={virtualItem.key}
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
                                        <div style={{ flex: 1.5, color: 'var(--text-muted)', fontSize: '0.9rem' }}>
                                            {book.publicationDate ? new Date(book.publicationDate).toLocaleDateString(undefined, {
                                                year: 'numeric',
                                                month: 'short',
                                                day: 'numeric'
                                            }) : 'TBA'}
                                        </div>
                                        <div style={{ flex: 1 }}>
                                            <span style={pageStyles.genreBadge}>{book.genre || 'Uncategorized'}</span>
                                        </div>
                                        <div style={{ flex: 1, textAlign: 'right', display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
                                            <Link to={`/books/performance/${book.id}`} style={pageStyles.actionIcon} title="Analytics">
                                                📊
                                            </Link>
                                            <button
                                                onClick={() => handleDelete(book.id, book.title)}
                                                style={pageStyles.deleteBtn}
                                                title="Delete"
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
        </div>
    );
};

const pageStyles = {
    container: { animation: 'fadeIn 0.3s ease' },
    header: { display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' },
    title: { fontSize: '1.875rem', fontWeight: '800', marginBottom: '0.25rem' },
    subtitle: { color: 'var(--text-muted)' },
    addBtn: { padding: '0.75rem 1.5rem', borderRadius: '10px' },
    searchCard: { padding: '1rem', marginBottom: '1.5rem', display: 'flex', alignItems: 'center' },
    searchWrapper: { position: 'relative' as const, flex: 1, display: 'flex', alignItems: 'center' },
    searchIcon: { position: 'absolute' as const, left: '1rem', color: '#94a3b8' },
    searchInput: {
        width: '100%',
        padding: '0.75rem 1rem 0.75rem 2.5rem',
        borderRadius: '8px',
        border: '1px solid var(--border)',
        fontSize: '1rem',
        outline: 'none',
    },
    tableCard: { padding: 0, overflow: 'hidden' },
    tableHeader: {
        display: 'flex',
        padding: '1rem 1.5rem',
        backgroundColor: '#f8fafc',
        borderBottom: '1px solid var(--border)',
        fontWeight: '700',
        color: '#64748b',
        textTransform: 'uppercase' as const,
        fontSize: '0.75rem',
        letterSpacing: '0.05em',
    },
    scrollArea: { height: '600px', overflow: 'auto', position: 'relative' as const },
    row: {
        position: 'absolute' as const,
        top: 0,
        left: 0,
        width: '100%',
        display: 'flex',
        alignItems: 'center',
        padding: '0 1.5rem',
        borderBottom: '1px solid var(--border)',
        boxSizing: 'border-box' as const,
        transition: 'background-color 0.2s',
    },
    bookTitle: { color: 'var(--text-main)', textDecoration: 'none', fontWeight: '700', fontSize: '1rem', display: 'block' },
    bookAuthor: { fontSize: '0.8rem', color: 'var(--text-muted)', marginTop: '2px' },
    genreBadge: {
        fontSize: '0.75rem',
        padding: '0.25rem 0.6rem',
        borderRadius: '20px',
        backgroundColor: '#f1f5f9',
        color: '#475569',
        fontWeight: '600'
    },
    actionIcon: { textDecoration: 'none', fontSize: '1.2rem', cursor: 'pointer', transition: 'transform 0.2s' },
    deleteBtn: { backgroundColor: 'transparent', border: 'none', fontSize: '1.2rem', cursor: 'pointer' },
    emptyState: { textAlign: 'center' as const, padding: '5rem 2rem', color: 'var(--text-muted)' },
    emptyIcon: { fontSize: '3rem', marginBottom: '1rem' },
    errorBanner: { padding: '1rem', backgroundColor: '#fef2f2', color: 'var(--danger)', borderRadius: '8px', marginBottom: '1rem', border: '1px solid #fee2e2' },
    centered: { padding: '10rem', textAlign: 'center' as const, color: 'var(--text-muted)', fontSize: '1.2rem' }
};

export default BookListPage;