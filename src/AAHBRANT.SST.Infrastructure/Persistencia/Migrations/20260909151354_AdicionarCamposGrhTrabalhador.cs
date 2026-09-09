using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AAHBRANT.SST.Infrastructure.Persistencia.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarCamposGrhTrabalhador : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Cep",
                table: "Trabalhadores",
                type: "nvarchar(9)",
                maxLength: 9,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ctps",
                table: "Trabalhadores",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataFimExperiencia1",
                table: "Trabalhadores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataFimExperiencia2",
                table: "Trabalhadores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataNascimento",
                table: "Trabalhadores",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Endereco",
                table: "Trabalhadores",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Municipio",
                table: "Trabalhadores",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NomeMae",
                table: "Trabalhadores",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pis",
                table: "Trabalhadores",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Salario",
                table: "Trabalhadores",
                type: "decimal(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Situacao",
                table: "Trabalhadores",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TamanhoBlusaEpi",
                table: "Trabalhadores",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TamanhoCalcaEpi",
                table: "Trabalhadores",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TamanhoCalcadoEpi",
                table: "Trabalhadores",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Uf",
                table: "Trabalhadores",
                type: "nvarchar(2)",
                maxLength: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Cep",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "Ctps",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "DataFimExperiencia1",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "DataFimExperiencia2",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "DataNascimento",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "Endereco",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "Municipio",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "NomeMae",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "Pis",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "Salario",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "Situacao",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "TamanhoBlusaEpi",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "TamanhoCalcaEpi",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "TamanhoCalcadoEpi",
                table: "Trabalhadores");

            migrationBuilder.DropColumn(
                name: "Uf",
                table: "Trabalhadores");
        }
    }
}
