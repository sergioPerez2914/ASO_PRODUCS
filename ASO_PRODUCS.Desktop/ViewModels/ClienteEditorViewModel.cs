using System;
using System.Linq;
using ASO_PRODUCS.Desktop.Models;
using ASO_PRODUCS.Desktop.Services;

namespace ASO_PRODUCS.Desktop.ViewModels;

/// <summary>
/// Alta/edición de un cliente. Calco de <see cref="ProveedorEditorViewModel"/>: el RIF (o
/// cédula) identifica al cliente ante el fisco, así que se valida que no se repita.
/// </summary>
public sealed class ClienteEditorViewModel : CrudEditorViewModelBase<Cliente>
{
    private readonly Cliente _original;
    private readonly IClienteDataSource _clientes;

    public ClienteEditorViewModel(Cliente original, IClienteDataSource clientes)
    {
        _original = original;
        _clientes = clientes;

        Nombre = original.Nombre;
        Rif = original.Rif;
        Telefono = original.Telefono;
        Notas = original.Notas;
        Activo = original.Activo;
    }

    public override string Titulo => _original.Id == 0 ? "Nuevo cliente" : $"Editar cliente Nº {_original.Id}";

    private string _nombre = string.Empty;
    public string Nombre
    {
        get => _nombre;
        set => SetProperty(ref _nombre, value);
    }

    private string _rif = string.Empty;
    public string Rif
    {
        get => _rif;
        set => SetProperty(ref _rif, value);
    }

    private string _telefono = string.Empty;
    public string Telefono
    {
        get => _telefono;
        set => SetProperty(ref _telefono, value);
    }

    private string _notas = string.Empty;
    public string Notas
    {
        get => _notas;
        set => SetProperty(ref _notas, value);
    }

    private bool _activo = true;
    public bool Activo
    {
        get => _activo;
        set => SetProperty(ref _activo, value);
    }

    protected override bool Validar(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            error = "Indique el nombre del cliente.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(Rif))
        {
            var repetido = _clientes.GetAll()
                .Any(c => c.Id != _original.Id
                          && string.Equals(c.Rif.Trim(), Rif.Trim(), StringComparison.OrdinalIgnoreCase));

            if (repetido)
            {
                error = $"Ya existe un cliente con el RIF {Rif.Trim()}.";
                return false;
            }
        }

        error = null;
        return true;
    }

    public override Cliente ObtenerResultado()
    {
        var cliente = _original.Clonar();
        cliente.Nombre = Nombre.Trim();
        cliente.Rif = Rif.Trim();
        cliente.Telefono = Telefono.Trim();
        cliente.Notas = Notas.Trim();
        cliente.Activo = Activo;
        return cliente;
    }
}
