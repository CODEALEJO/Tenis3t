// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
// Para cargar tallas dinámicamente cuando se selecciona un producto
$(document).ready(function() {
    $('#ProductoId').change(function() {
        var productoId = $(this).val();
        if (productoId) {
            $.getJSON('/Prestamo/GetTallasByProducto', { productoId: productoId }, function(data) {
                $('#TallaInventarioId').empty();
                $.each(data, function(index, item) {
                    $('#TallaInventarioId').append(
                        $('<option></option>').attr('value', item.id).text(item.talla + ' (Disponibles: ' + item.cantidad + ')')
                    );
                });
            });
        }
    });
});