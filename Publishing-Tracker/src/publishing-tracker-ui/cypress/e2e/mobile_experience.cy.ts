/// <reference types="cypress" />

describe('Mobile Experience', () => {
    beforeEach(() => {
        cy.viewport('iphone-6');
        cy.login('test@test.com', 'password123');
    });

    it('should display a responsive dashboard', () => {
        cy.visit('/dashboard');
        cy.get('.summary-grid').should('have.css', 'grid-template-columns', 'repeat(1, minmax(0px, 1fr))');
    });

    it('should display a responsive add book form', () => {
        cy.visit('/books/add');
        cy.get('.form-container').should('be.visible');
    });
});