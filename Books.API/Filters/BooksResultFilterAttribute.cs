using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Books.API.Filters
{
    public class BooksResultFilterAttribute : ResultFilterAttribute
    {
        //private readonly IMapper _mapper;
        //public BookResultFilterAttribute(IMapper mapper) 
        //{
        //    _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        //}

        public override async Task OnResultExecutionAsync(ResultExecutingContext context, 
            ResultExecutionDelegate next)
        {
            var resultFromAction = context.Result as ObjectResult;
            if (resultFromAction?.Value == null
                || resultFromAction.StatusCode < 200
                || resultFromAction.StatusCode >= 300)
            {
                await next();
                return;
            }

            var mapper = context.HttpContext.RequestServices.GetRequiredService<IMapper>();

            resultFromAction.Value = mapper.Map<IEnumerable<Models.Book>>(resultFromAction.Value);
            
            await next();
        }
    }
}
