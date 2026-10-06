global using System.Security.Claims;
global using System.Text;
global using System.Threading.RateLimiting;
global using Microsoft.AspNetCore.Authentication.JwtBearer;
global using Microsoft.AspNetCore.Authorization;
global using Microsoft.AspNetCore.Http;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.Configuration;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.DependencyInjection.Extensions;
global using Microsoft.Extensions.Options;
global using Microsoft.IdentityModel.Tokens;
global using Microsoft.AspNetCore.Builder;


global using CoreKit.IAM.Common;
global using CoreKit.IAM.Constants;
global using CoreKit.IAM.Entities;
global using CoreKit.IAM.Interfaces;
global using CoreKit.IAM.Models;
global using CoreKit.IAM.Normalization;
global using CoreKit.IAM.Persistence;
global using CoreKit.IAM.Security;
global using CoreKit.IAM.Services;
global using CoreKit.IAM.Settings;
global using CoreKit.IAM.Validation;

global using Microsoft.AspNetCore.WebUtilities;
global using Microsoft.IdentityModel.JsonWebTokens;
global using System.Security.Cryptography;
global using System.Text.Json;
global using Microsoft.AspNetCore.Identity;

global using CoreKit.IAM.Authorization;
global using CoreKit.IAM.Hooks;
global using CoreKit.IAM.Seeding;