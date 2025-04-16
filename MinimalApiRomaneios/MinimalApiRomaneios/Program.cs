using FirebirdSql.Data.FirebirdClient;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Configurar serviços (incluindo o Swagger)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    // Configuração do Swagger para aceitar o Bearer Token
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Entre com o seu token Bearer",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

var app = builder.Build();

// Verificar se a aplicação está configurada corretamente

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MinimalApiRomaneios v1");
        c.RoutePrefix = string.Empty; // Deixa o Swagger disponível na raiz do projeto
    });


app.MapGet("/romaneios", async (int cdRomaneio, HttpContext context) =>
{
    // Caminho do arquivo de configuração
    var configFilePath = Path.Combine(Directory.GetCurrentDirectory(), "config.txt");

    // Verifica se o arquivo de configuração existe
    if (!File.Exists(configFilePath))
    {
        return Results.Problem("Config file not found.", statusCode: 500);
    }

    // Lê o conteúdo do arquivo
    var configLines = await File.ReadAllLinesAsync(configFilePath);

    // A primeira linha é a connectionString, a segunda linha é o token válido
    var connectionString = configLines.Length > 1 ? configLines[1] : string.Empty;
    var validToken = configLines.Length > 0 ? configLines[0] : string.Empty;

    // Verifica se o token no header é válido
    var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", string.Empty);
    if (token != validToken)
    {
        return Results.Problem("Invalid token.", statusCode: 401);
    }

    // Consulta SQL com parâmetro
    var query = @"
        SELECT R1.CDROMANEIO,
               CASE WHEN R1.CDORCAMENTO IS NULL THEN 'VD' ELSE 'PV' END AS TIPO,
               R1.DOCUMENTO, R1.QTDE_VOL, R1.IDCOMPANHIA, R1.IDFANTASIA, R1.NMRAZAO,
               R1.NMENDERECO, R1.NRNUMERO, R1.NMCOMPLEMENTO, R1.NMBAIRRO, R1.IDCEP, R1.NMCIDADE, R1.IDESTADO, R1.IDLATITUDE, R1.IDLONGITUDE
        FROM(
            SELECT R.CDROMANEIO, CDORCAMENTO, VD.CDVENDA AS CDVENDA, VD.iddoc AS DOCUMENTO, C.cdcompanhia, C.idcompanhia,
                   C.idfantasia, C.nmrazao, CAST(VD.qtunfrete AS INTEGER) as QTDE_vol, 
                   E.cdendereco, E.nmendereco, E.nrnumero, E.nmcomplemento, E.nmbairro, 
                   E.idcep, CI.nmcidade, UF.idestado, E.idlatitude, E.idlongitude
            FROM CMROMANEIO R
            INNER JOIN CMROMANEIOVENDA RV ON RV.CDROMANEIO=R.CDROMANEIO
            INNER JOIN CMVENDA VD ON VD.CDVENDA=RV.CDVENDA
            INNER JOIN CMCOMPANHIA C ON C.cdcompanhia=VD.CDCLIENTE
            INNER JOIN cmenderecocli E ON E.cdendereco = VD.cdenderecoentrega
            INNER JOIN cmcidade CI ON CI.cdcidade = E.cdcidade
            INNER JOIN cmestado UF ON UF.cdestado = CI.cdestado
            WHERE R.CDROMANEIO = @CdRomaneio

            UNION ALL

            SELECT R.CDROMANEIO, O.CDORCAMENTO, NULL AS CDVENDA, O.idpedvenda AS DOCUMENTO, C.cdcompanhia, C.idcompanhia,
                   C.idfantasia, C.nmrazao, CAST(o.qtunfrete AS INTEGER) as QTDE_vol,
                   E.cdendereco, E.nmendereco, E.NRNUMERO,E.nmcomplemento, E.nmbairro, 
                   E.idcep, CI.nmcidade, UF.idestado, E.idlatitude, E.idlongitude
            FROM CMROMANEIO R
            INNER JOIN CMROMANEIOorc RO ON RO.CDROMANEIO=R.CDROMANEIO
            INNER JOIN CMORCAMENTO O ON O.CDORCAMENTO=RO.CDORCAMENTO
            INNER JOIN CMCOMPANHIA C ON C.cdcompanhia=O.CDCLIENTE
            INNER JOIN cmenderecocli E ON E.cdendereco = O.cdenderecoentrega
            INNER JOIN cmcidade CI ON CI.cdcidade = E.cdcidade
            INNER JOIN cmestado UF ON UF.cdestado = CI.cdestado
            WHERE R.CDROMANEIO = @CdRomaneio
              AND NOT EXISTS(
                  SELECT VOP.CDVENDA
                  FROM CMVENDAOPS VOP
                  WHERE VOP.cdpedvenda=O.cdorcamento
              )
        ) R1
        ORDER BY R1.DOCUMENTO";

    using var connection = new FbConnection(connectionString);
    await connection.OpenAsync();

    using var command = new FbCommand(query, connection);
    command.Parameters.AddWithValue("@CdRomaneio", cdRomaneio);

    using var reader = await command.ExecuteReaderAsync();

    var results = new List<object>();

    while (await reader.ReadAsync())
    {
        results.Add(new
        {
            CDROMANEIO = reader["CDROMANEIO"],
            TIPO = reader["TIPO"],
            DOCUMENTO = reader["DOCUMENTO"],
            QTDE_VOL = reader["QTDE_VOL"],
            IDCOMPANHIA = reader["IDCOMPANHIA"],
            IDFANTASIA = reader["IDFANTASIA"],
            NMRAZAO = reader["NMRAZAO"],
            NMENDERECO = reader["NMENDERECO"],
            NRNUMERO = reader["NRNUMERO"],
            NMCOMPLEMENTO = reader["NMCOMPLEMENTO"],
            NMBAIRRO = reader["NMBAIRRO"],
            IDCEP = reader["IDCEP"],
            NMCIDADE = reader["NMCIDADE"],
            IDESTADO = reader["IDESTADO"],
            IDLATITUDE = reader["IDLATITUDE"],
            IDLONGITUDE = reader["IDLONGITUDE"]
        });
    }

    return Results.Ok(results);
});

// Inicia a aplicação
app.Run();
