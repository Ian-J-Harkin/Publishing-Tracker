import { useEffect, useState, useRef } from 'react';
import { Link } from 'react-router-dom';
import { bookService } from '../services/bookService';
import { Book } from '../types/book';
import { useVirtualizer } from '@tanstack/react-virtual';

const BookListPage = () => {
    const [books, setBooks] = useState<Book[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);
    const parentRef = useRef<HTMLDivElement>(null);

    const rowVirtualizer = useVirtualizer({
        count: books.length,
        getScrollElement: () => parentRef.current,
        estimateSize: () => 60,
    });

    useEffect(() => {
        const fetchBooks = async () => {
            try {
                const booksData = await bookService.getBooks();
                setBooks(booksData);
            } catch {
                setError('Failed to fetch books.');
            } finally {
                setLoading(false);
            }
        };
        fetchBooks();
    }, []);

    const handleDelete = async (id: number) => {
        if (window.confirm('Are you sure you want to delete this book?')) {
            try {
                await bookService.deleteBook(id);
                setBooks(books.filter(book => book.id !== id));
            } catch {
                setError('Failed to delete book.');
            }
        }
    };

    if (loading) return <div className="loading">Loading library...</div>;

    return (
        <div>
            {/* Header Area using Flexbox directly for layout */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
                <h1>My Books</h1>
                <Link to="/books/add" className="btn-primary">
                    + Add New Book
                </Link>
            </div>

            {error && <div style={{ color: 'var(--danger)', marginBottom: '1rem' }}>{error}</div>}

            {/* Main Content Card */}
            <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
                {/* Reusable Table Header Style */}
                <div style={pageStyles.tableHeader}>
                    <div style={{ flex: 2 }}>Title</div>
                    <div style={{ flex: 1 }}>Author</div>
                    <div style={{ flex: 1, textAlign: 'right' }}>Actions</div>
                </div>

                {/* Scrollable Virtual Area */}
                <div ref={parentRef} style={{ height: '600px', overflow: 'auto', position: 'relative' }}>
                    <div style={{ height: `${rowVirtualizer.getTotalSize()}px`, width: '100%', position: 'relative' }}>
                        {rowVirtualizer.getVirtualItems().map(virtualItem => {
                            const book = books[virtualItem.index];
                            return (
                                <div
                                    key={virtualItem.key}
                                    style={{
                                        ...pageStyles.row,
                                        height: `${virtualItem.size}px`,
                                        transform: `translateY(${virtualItem.start}px)`
                                    }}
                                >
                                    <div style={{ flex: 2, fontWeight: '600' }}>
                                        <Link to={`/books/edit/${book.id}`} style={pageStyles.bookLink}>
                                            {book.title}
                                        </Link>
                                    </div>
                                    <div style={{ flex: 1, color: 'var(--text-muted)' }}>
                                        {book.author}
                                    </div>
                                    <div style={{ flex: 1, textAlign: 'right' }}>
                                        <button
                                            onClick={() => handleDelete(book.id)}
                                            style={pageStyles.deleteBtn}
                                        >
                                            Delete
                                        </button>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </div>
            </div>
        </div>
    );
};

const pageStyles = {
    tableHeader: {
        display: 'flex',
        padding: '1rem 1.5rem',
        backgroundColor: '#f1f5f9',
        borderBottom: '1px solid var(--border)',
        fontWeight: '700',
        color: 'var(--text-muted)',
        textTransform: 'uppercase' as const,
        fontSize: '0.75rem',
        letterSpacing: '0.05em',
    },
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
    },
    bookLink: {
        color: 'var(--text-main)',
        textDecoration: 'none',
    },
    deleteBtn: {
        backgroundColor: 'transparent',
        border: 'none',
        color: 'var(--danger)',
        cursor: 'pointer',
        fontSize: '0.85rem',
        fontWeight: '600',
    }
};

export default BookListPage;