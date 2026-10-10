import { countryName, label, ownerLabel, statusLabel, typeLabel } from './inventory-labels';

describe('inventory labels', () => {
  it('translates the normalized values', () => {
    expect(typeLabel('server')).toBe('Servidor');
    expect(typeLabel('workstation')).toBe('Estación de trabajo');
    expect(statusLabel('pending')).toBe('Pendiente');
    expect(label('encryption', 'not-encrypted')).toBe('Sin cifrar');
    expect(label('busType', 'nvme')).toBe('NVMe');
    expect(label('section', 'network.publicNetwork')).toBe('IP pública');
  });

  it('never invents a value (SPEC-0002 RN-6)', () => {
    expect(label('firewall', null)).toBe('No disponible');
    expect(label('mediaType', 'optical')).toBe('optical');
  });

  it('shows the collaborator name, else the e-mail, else the definition wording', () => {
    expect(ownerLabel({ email: 'ana@example.com', name: 'Ana Pérez' })).toBe('Ana Pérez');
    expect(ownerLabel({ email: 'ana@example.com', name: null })).toBe('ana@example.com');
    expect(ownerLabel(null)).toBe('Sin usuario asociado');
  });

  it('names countries in Spanish', () => {
    expect(countryName('CO')).toBe('Colombia');
    expect(countryName(null)).toBeNull();
  });
});
